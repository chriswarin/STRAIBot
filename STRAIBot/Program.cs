using Scalar.AspNetCore;
using STRAIBot.Services;
using STRAIBot.Services.Guesty;
using STRAIBot.Services.Messaging;
using STRAIBot.Services.Properties;
using STRAIBot.Services.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((doc, _, _) =>
    {
        doc.Info.Title = "STRAIBot API";
        doc.Info.Version = "v1";
        doc.Info.Description =
           "Event-driven AI guest communication platform for short-term rentals. " +
            "Combines PMS webhooks, structured GBrain-style operational memory retrieval, " +
            "and LLM reasoning to generate context-aware guest messaging, risk classification, " +
            "policy enforcement, and autonomous escalation workflows.";
        return Task.CompletedTask;
    });
});

// ── Property memory retrieval ─────────────────────────────────────────────────
// MarkdownPropertyMemoryService reads local memory/{PropertyName}/*.md files.
// MemoryContextService routes between Markdown and GBrain based on Memory:Provider config.
// Switch Memory:Provider to "GBrain" in appsettings.json to activate GBrain retrieval.
builder.Services.AddSingleton<IPropertyMemoryService, MarkdownPropertyMemoryService>();
builder.Services.AddScoped<IMemoryContextService, MemoryContextService>();

// ── GBrain HTTP client ────────────────────────────────────────────────────────
// Configured via Memory:GBrainBaseUrl in appsettings.json (default: http://localhost:8088).
// Safe to run locally without GBrain — all errors fall back to Markdown.
builder.Services.AddHttpClient<IGBrainClient, GBrainClient>();

// ── Risk classification (safety fallback — may be removed after OpenAI validation) ──
builder.Services.AddSingleton<IMessageRiskClassifier, MessageRiskClassifier>();

// ── Host notifications ────────────────────────────────────────────────────────
// Currently logs to console. Replace with email/SMS/Guesty delivery later.
builder.Services.AddScoped<IHostNotificationService, HostNotificationService>();

// ── AI decision pipeline ──────────────────────────────────────────────────────
// AiGuestMessageDecisionService: local placeholder today, OpenAI call tomorrow.
// AiDecisionValidator: C# safety guardrail — never removed, always runs.
builder.Services.AddScoped<IAiGuestMessageDecisionService, AiGuestMessageDecisionService>();
builder.Services.AddScoped<IAiDecisionValidator, AiDecisionValidator>();

// ── Draft response orchestration ─────────────────────────────────────────────
builder.Services.AddScoped<IDraftResponseService, DraftResponseService>();

// ── Guesty API clients ────────────────────────────────────────────────────────
// GuestyReservationClient: fetches reservation details for property resolution.
// GuestyMessageClient: sends outbound replies into Guesty conversation threads.
// Both are safe to run without a token — they log a warning and skip HTTP calls.
builder.Services.AddHttpClient<IGuestyReservationClient, GuestyReservationClient>();
builder.Services.AddHttpClient<IGuestyMessageClient, GuestyMessageClient>();

// ── Property resolution ───────────────────────────────────────────────────────
// PropertyMappingService: loaded once at startup, validates duplicates.
//   - GuestyListingId  → how Guesty knows the property
//   - PropertyKey      → how STRAIBot knows the property (memory folder, business logic)
//   - GBrainMemoryKey  → how GBrain scopes vector memory for the property
// PropertyResolver: fetches Guesty reservation → extracts GuestyListingId → returns PropertyMapping.
builder.Services.AddSingleton<IPropertyMappingService, PropertyMappingService>();
builder.Services.AddScoped<IPropertyResolver, PropertyResolver>();

// ── HTML message cleaning ─────────────────────────────────────────────────────
// Converts Guesty HTML message bodies to plain text for AI processing.
builder.Services.AddSingleton<IHtmlMessageCleaner, HtmlMessageCleaner>();

// ── Webhook queue, deduplication, and background processor ───────────────────
// Queue: singleton channel shared between controller (writer) and processor (reader).
// ProcessedMessageStore: in-memory dedup. FUTURE: replace with Redis/SQL for persistence.
// GuestyWebhookProcessor: BackgroundService — drains queue, runs full pipeline.
builder.Services.AddSingleton<IWebhookMessageQueue, InMemoryWebhookMessageQueue>();
builder.Services.AddSingleton<IProcessedMessageStore, InMemoryProcessedMessageStore>();
builder.Services.AddHostedService<GuestyWebhookProcessor>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "STRAIBot API";
        options.Theme = ScalarTheme.BluePlanet;
        options.DefaultHttpClient = new(ScalarTarget.Http, ScalarClient.HttpClient);
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

