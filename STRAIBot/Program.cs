using Scalar.AspNetCore;
using STRAIBot.Services;
using STRAIBot.Services.GBrain;
using STRAIBot.Services.Guesty;
using STRAIBot.Services.Memory;
using STRAIBot.Services.Messaging;
using STRAIBot.Services.OpenAI;
using STRAIBot.Services.Properties;
using STRAIBot.Services.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialize enums as strings (e.g. "HardRule" not 0, "Emergency" not 3)
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });


// ── Property memory retrieval ─────────────────────────────────────────────────
// MarkdownPropertyMemoryService reads local memory/{PropertyKey}/**/*.md files.
// MemoryContextService routes between GBrain and Markdown based on Memory:Provider config.
//
// Memory:Provider options:
//   "GBrain"    → query local GBrain MCP server at Memory:GBrainBaseUrl first
//   "Markdown"  → read local markdown files only
//
// Memory:FallbackToMarkdown = true means GBrain failure/empty → fall back to Markdown.
// Set Memory:Provider = "Markdown" to bypass GBrain entirely (offline dev).
builder.Services.AddSingleton<IPropertyMemoryService, MarkdownPropertyMemoryService>();
builder.Services.AddScoped<IMemoryContextService, MemoryContextService>();

// ── GBrain HTTP client ────────────────────────────────────────────────────────
// Connects to local GBrain MCP server (default: http://localhost:3131).
// Health:  GET  /health
// Search:  POST /mcp  (MCP tool-call, tool name: "search")
// Admin:   GET  /admin  ← use this to verify tool names and argument keys
// Safe to run without GBrain — all errors return empty string and fall back to Markdown.
builder.Services.AddHttpClient<IGBrainClient, GBrainClient>();

// ── Risk classification (safety fallback — may be removed after OpenAI validation) ──
builder.Services.AddSingleton<IMessageRiskClassifier, MessageRiskClassifier>();

// ── Host notifications ────────────────────────────────────────────────────────
// Currently logs to console. Replace with email/SMS/Guesty delivery later.
builder.Services.AddScoped<IHostNotificationService, HostNotificationService>();

// ── AI decision pipeline ──────────────────────────────────────────────────────
// OpenAiDecisionClient: calls GPT-4o with system prompt + GBrain context.
//   Configured via OpenAI:ApiKey and OpenAI:Model in appsettings.Development.json.
//   Returns null on failure — AiGuestMessageDecisionService falls back to local placeholder.
// AiGuestMessageDecisionService: tries OpenAI first, falls back to keyword-based placeholder.
// AiDecisionValidator: C# safety guardrail — always runs, never removed.
builder.Services.AddSingleton<IOpenAiDecisionClient, OpenAiDecisionClient>();
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

