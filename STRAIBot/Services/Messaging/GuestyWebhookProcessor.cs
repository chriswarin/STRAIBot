using STRAIBot.Models.Guesty;
using STRAIBot.Services.Guesty;
using STRAIBot.Services.Properties;
using STRAIBot.Services.Text;

namespace STRAIBot.Services.Messaging;

/// <summary>
/// BackgroundService that drains IWebhookMessageQueue and runs the full
/// receive → think → respond pipeline for each inbound Guesty guest message.
///
/// Pipeline per message:
///   1. Extract messageId — skip if already processed (dedup)
///   2. Resolve propertyKey from reservationId via IPropertyResolver
///   3. Clean HTML body to plain text via IHtmlMessageCleaner
///   4. Build ProcessedGuestMessage record
///   5. Call DraftResponseService.GenerateDraftAsync
///   6. Evaluate auto-send gate (Enabled + DryRunMode flags)
///   7. If live send: call IGuestyMessageClient.SendMessageAsync
///   8. Mark messageId as processed
///   9. Structured logging of every decision and send outcome
///
/// FUTURE enhancements:
///   - Database-backed ProcessedMessageStore for restart-safe deduplication
///   - Retry queue for failed sends
///   - Dead-letter queue for unresolvable messages
///   - AI confidence threshold gates before auto-send
///   - Host escalation notification integration
///   - GBrain memory retrieval integration
///   - OpenAI decision generation integration
/// </summary>
public sealed class GuestyWebhookProcessor : BackgroundService
{
    private readonly IWebhookMessageQueue _queue;
    private readonly IProcessedMessageStore _processedStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHtmlMessageCleaner _cleaner;
    private readonly IConfiguration _config;
    private readonly ILogger<GuestyWebhookProcessor> _logger;

    public GuestyWebhookProcessor(
        IWebhookMessageQueue queue,
        IProcessedMessageStore processedStore,
        IServiceScopeFactory scopeFactory,
        IHtmlMessageCleaner cleaner,
        IConfiguration config,
        ILogger<GuestyWebhookProcessor> logger)
    {
        _queue = queue;
        _processedStore = processedStore;
        _scopeFactory = scopeFactory;
        _cleaner = cleaner;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GuestyWebhookProcessor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var envelope = await _queue.DequeueAsync(stoppingToken);
                await ProcessAsync(envelope, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break; // Normal shutdown
            }
            catch (Exception ex)
            {
                // Never let one bad message kill the processor loop
                _logger.LogError(ex, "Unhandled error in GuestyWebhookProcessor loop.");
            }
        }

        _logger.LogInformation("GuestyWebhookProcessor stopped.");
    }

    private async Task ProcessAsync(GuestyWebhookEnvelope envelope, CancellationToken cancellationToken)
    {
        var reservationId = envelope.ReservationId;
        var messageId = envelope.Data?.MessageId ?? string.Empty;
        var conversationId = envelope.Data?.ConversationId ?? string.Empty;
        var module = envelope.Data?.Module ?? "email";
        var guestName = envelope.Data?.GuestName ?? string.Empty;

        // ── Step 1: Duplicate detection ───────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(messageId) && _processedStore.HasProcessed(messageId))
        {
            _logger.LogInformation(
                "Duplicate webhook skipped | MessageId={MessageId} | ReservationId={ReservationId}",
                messageId, reservationId);
            return;
        }

        // Scoped services need a fresh scope per message
        await using var scope = _scopeFactory.CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IPropertyResolver>();
        var draftService = scope.ServiceProvider.GetRequiredService<IDraftResponseService>();
        var messageClient = scope.ServiceProvider.GetRequiredService<IGuestyMessageClient>();

        // ── Step 2: Resolve property mapping ─────────────────────────────────
        var mapping = await resolver.ResolveAsync(envelope, cancellationToken);

        if (mapping is null)
        {
            _logger.LogWarning(
                "Property unresolved | ReservationId={ReservationId} | MessageId={MessageId}. " +
                "Message will NOT be auto-processed. Host review required. " +
                "Add a PropertyMapping with the correct GuestyListingId in appsettings.json.",
                reservationId, messageId);

            // Mark processed so we don't retry an unresolvable message indefinitely
            // FUTURE: route to dead-letter queue for manual investigation
            MarkProcessedIfKnown(messageId);
            return;
        }

        var propertyKey = mapping.PropertyKey;
        var gBrainMemoryKey = mapping.GBrainMemoryKey;

        // ── Step 3: Clean guest message body ──────────────────────────────────
        var rawBody = !string.IsNullOrWhiteSpace(envelope.Data?.PlainText)
            ? envelope.Data.PlainText
            : envelope.Data?.Body;

        var plainTextMessage = _cleaner.Clean(rawBody);

        if (string.IsNullOrWhiteSpace(plainTextMessage))
        {
            _logger.LogWarning(
                "Empty message body after cleaning | ReservationId={ReservationId} | " +
                "Property={PropertyKey} | MessageId={MessageId}",
                reservationId, propertyKey, messageId);
            MarkProcessedIfKnown(messageId);
            return;
        }

        _logger.LogInformation(
            "Processing guest message | ReservationId={ReservationId} | " +
            "Property={PropertyKey} | GBrainMemoryKey={GBrainMemoryKey} | " +
            "ConversationId={ConversationId} | MessageId={MessageId} | " +
            "Guest={GuestName} | Preview={Preview}",
            reservationId, propertyKey, gBrainMemoryKey, conversationId, messageId, guestName,
            plainTextMessage.Length > 120 ? plainTextMessage[..120] + "…" : plainTextMessage);

        // ── Step 4: Generate AI decision ──────────────────────────────────────
        var result = await draftService.GenerateDraftAsync(propertyKey, plainTextMessage);

        // ── Step 5: Log the full decision ─────────────────────────────────────
        _logger.LogInformation(
            "Decision | ReservationId={ReservationId} | Property={PropertyKey} | " +
            "GBrainMemoryKey={GBrainMemoryKey} | Risk={Risk} | Policy={Policy} | " +
            "Category={Category} | Confidence={Confidence} | AutoSend={AutoSend} | " +
            "HostReview={HostReview} | NotifyHost={NotifyHost} | EscalationReason={EscalationReason}",
            reservationId, propertyKey, gBrainMemoryKey,
            result.RiskLevel, result.PolicyRuleType, result.Category,
            result.Confidence, result.ShouldAutoSend, result.RequiresHostReview,
            result.ShouldNotifyHost, result.EscalationReason ?? "none");

        _logger.LogInformation(
            "GuestResponse | ReservationId={ReservationId} | Response={Response}",
            reservationId, result.GuestResponse);

        // ── Step 6: Evaluate auto-send gate ───────────────────────────────────
        var autoSendEnabled = _config.GetValue<bool>("AutoSendMessaging:Enabled", false);
        var dryRunMode = _config.GetValue<bool>("AutoSendMessaging:DryRunMode", true);

        var shouldSend = autoSendEnabled
                         && result.ShouldAutoSend
                         && !result.RequiresHostReview
                         && !string.IsNullOrWhiteSpace(result.GuestResponse)
                         && !string.IsNullOrWhiteSpace(conversationId);

        if (!autoSendEnabled)
        {
            _logger.LogInformation(
                "Auto-send DISABLED | ReservationId={ReservationId} | " +
                "Would have sent: '{Response}'",
                reservationId, result.GuestResponse);
        }
        else if (!result.ShouldAutoSend || result.RequiresHostReview)
        {
            _logger.LogInformation(
                "Auto-send skipped — requires host review | " +
                "ReservationId={ReservationId} | ShouldAutoSend={AutoSend} | " +
                "RequiresHostReview={HostReview} | EscalationReason={Reason}",
                reservationId, result.ShouldAutoSend, result.RequiresHostReview,
                result.EscalationReason ?? "none");
        }
        else if (string.IsNullOrWhiteSpace(conversationId))
        {
            _logger.LogWarning(
                "Auto-send skipped — no conversationId in webhook payload | " +
                "ReservationId={ReservationId}",
                reservationId);
        }
        else if (dryRunMode)
        {
            // ── Step 7a: Dry-run — simulate send without calling Guesty API ──
            _logger.LogInformation(
                "DRY-RUN send | ReservationId={ReservationId} | " +
                "ConversationId={ConversationId} | Module={Module} | Response='{Response}'",
                reservationId, conversationId, module, result.GuestResponse);
        }
        else if (shouldSend)
        {
            // ── Step 7b: Live send — call Guesty API ─────────────────────────
            _logger.LogInformation(
                "LIVE send | ReservationId={ReservationId} | " +
                "ConversationId={ConversationId} | Module={Module}",
                reservationId, conversationId, module);

            await messageClient.SendMessageAsync(
                conversationId,
                result.GuestResponse,
                module,
                cancellationToken);
        }

        // ── Step 8: Mark message as processed ─────────────────────────────────
        MarkProcessedIfKnown(messageId);

        _logger.LogInformation(
            "Webhook processing complete | ReservationId={ReservationId} | " +
            "MessageId={MessageId} | Property={PropertyKey}",
            reservationId, messageId, propertyKey);
    }

    private void MarkProcessedIfKnown(string messageId)
    {
        if (!string.IsNullOrWhiteSpace(messageId))
            _processedStore.MarkProcessed(messageId);
    }
}

