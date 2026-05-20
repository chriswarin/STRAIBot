# STRAIBot

**AI-native operational messaging platform for short-term rentals.**

STRAIBot is a **webhook-driven AI orchestration runtime** — not a chatbot wrapper. It receives inbound PMS events, resolves property context via scoped memory retrieval (**GBrain**), routes decisions through a structured AI reasoning layer (**OpenAI**), enforces operational policy in C#, and either autonomously replies or escalates to a human — all as a composable, API-first system.

```
PMS Webhook → Property Resolution → GBrain Retrieval → OpenAI Decision → Policy Validation → Send or Escalate
```

> Built on ASP.NET Core 10 · OpenAPI 3.1 · Scalar · GBrain · OpenAI structured outputs · Guesty Open API

---

## Why This Exists

Short-term rental operations are fundamentally **repetitive, time-sensitive, policy-bound messaging work**:

- Guests ask the same questions across hundreds of reservations
- Policy violations require immediate, accurate responses
- Emergencies demand instant escalation and acknowledgement
- Hosts cannot afford to be available 24/7 across 5, 50, or 500 properties

Traditional approaches — templated autoresponders, generic chatbots, offshore VAs — all fail at the same boundary: **they have no operational memory and no policy understanding**.

STRAIBot treats guest messaging as an **event-driven orchestration problem**, not a conversation problem:

| Traditional Approach | STRAIBot Approach |
|---|---|
| Template-matching keyword rules | Semantic retrieval + structured reasoning |
| Generic chatbot | Property-scoped operational memory |
| Static FAQ responses | AI decision with policy validation |
| Manual escalation | Automated risk classification + routing |
| One-size-fits-all responses | Per-property memory namespaces |

The architecture draws directly from retrieval-first AI systems, operational agent design, and scoped memory patterns — the same ideas driving modern AI infrastructure at scale.

---

## Core Architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│                         INGESTION LAYER                              │
│                                                                      │
│       Guesty Webhook                  Manual API / Admin UI          │
│   POST /api/webhooks/guesty           POST /api/draft-response       │
└──────────────────┬────────────────────────────┬─────────────────────┘
                   │                            │
                   ▼                            ▼
┌──────────────────────────────────────────────────────────────────────┐
│                       ORCHESTRATION LAYER                            │
│                                                                      │
│   Webhook Queue (Channel<T>)     Dedup Store (message ID)            │
│                    │                                                 │
│         GuestyWebhookProcessor (BackgroundService)                   │
└──────────────────────────────────┬───────────────────────────────────┘
                                   │
                   ┌───────────────▼────────────────┐
                   │         RESOLUTION LAYER        │
                   │                                 │
                   │  GuestyReservationClient        │
                   │    GET /reservations/{id}       │
                   │    → extract GuestyListingId    │
                   │                                 │
                   │  PropertyMappingService         │
                   │    GuestyListingId              │
                   │    → PropertyKey                │
                   │    → GBrainMemoryKey            │
                   └───────────────┬────────────────┘
                                   │
                   ┌───────────────▼────────────────┐
                   │           MEMORY LAYER          │
                   │                                 │
                   │  GBrainClient                   │
                   │    POST {base}/query            │
                   │    scope: GBrainMemoryKey       │
                   │    → semantic retrieval         │
                   │    → property-scoped context    │
                   │                                 │
                   │  Fallback: MarkdownMemory       │
                   └───────────────┬────────────────┘
                                   │
                   ┌───────────────▼────────────────┐
                   │        AI REASONING LAYER       │
                   │                                 │
                   │  AiGuestMessageDecisionService  │
                   │    system prompt + context      │
                   │    → OpenAI structured output   │
                   │    → AiGuestMessageDecision     │
                   └───────────────┬────────────────┘
                                   │
                   ┌───────────────▼─────────────────┐
                   │      POLICY VALIDATION LAYER     │
                   │                                  │
                   │  AiDecisionValidator (C#)        │
                   │    emergency override            │
                   │    legal escalation              │
                   │    approval phrase detection     │
                   │    confidence gate               │
                   └──────────┬───────────┬──────────┘
                              │           │
              ┌───────────────▼──┐   ┌───▼──────────────────┐
              │  OUTBOUND SEND   │   │  ESCALATION ROUTING   │
              │                  │   │                       │
              │  GuestyMessage   │   │  HostNotification     │
              │  Client          │   │  Service              │
              │  /send-message   │   │  → alert / SMS        │
              │  DryRun gate     │   │  → RequiresHostReview │
              └──────────────────┘   └───────────────────────┘
```

---

## System Responsibilities

### STRAIBot — Orchestration Runtime

STRAIBot owns the **operational pipeline**. It does not generate content, retrieve memory, or make policy decisions directly — it orchestrates the systems that do.

- Receive and acknowledge PMS webhooks with immediate `200 OK`
- Enqueue events for background processing (no inline AI work on the HTTP thread)
- Resolve reservations → listings → internal property keys
- Route retrieved context and guest messages to the AI reasoning layer
- Enforce safety invariants in C# regardless of AI output
- Gate outbound sends via `AutoSendMessaging:Enabled` + `DryRunMode`
- Route escalations to the host notification system
- Deduplicate Guesty webhook retries via message ID tracking

### GBrain — Operational Memory Layer

GBrain is a **scoped vector memory system**. Each property has a dedicated memory namespace (`GBrainMemoryKey`) where operational knowledge is stored as semantic embeddings.

```
property:cozy-crab/     →  amenities, house-rules, parking, common-responses
property:blue-horizon/  →  amenities, house-rules, hot-tub, common-responses
property:turquoise-bay/ →  amenities, house-rules, beach-items, common-responses
```

When a guest message arrives, GBrain retrieves only the **semantically relevant slice** of that property's knowledge — not everything, just what matters for this specific message.

**GBrain retrieves context. It does not generate guest responses.**

Planned query shape:

```json
{
  "propertyKey":     "CozyCrab",
  "gBrainMemoryKey": "property:cozy-crab",
  "guestMessage":    "Is the pool heated?",
  "topK":            5
}
```

When GBrain is unavailable, `MemoryContextService` falls back to local Markdown files automatically.

### OpenAI — Structured Reasoning Layer

OpenAI receives a structured prompt containing the guest message and retrieved property context, and returns a **typed JSON decision** — not free-form text.

The system prompt instructs the model to:
- Use only the provided property context
- Never invent amenities, exceptions, or policy changes
- Never approve pets, parties, extra guests, late checkout, or refunds
- Classify emergency messages and require host notification
- Return valid JSON matching `AiGuestMessageDecision`

```
System Prompt + PropertyName + GuestMessage + RetrievedContext (GBrain)

                          ▼  OpenAI  ▼

AiGuestMessageDecision {
  category, riskLevel, policyRuleType,
  shouldAutoSend, requiresHostReview, shouldNotifyHost,
  confidence, guestResponse, matchedPolicy,
  reasoningSummary, escalationReason
}
```

**OpenAI output is always validated by the C# policy layer before any message is sent.**

---

## Property Memory Model

Each property maps three distinct identifier types:

```json
{
  "PropertyKey":     "CozyCrab",
  "DisplayName":     "Cozy Crab",
  "GBrainMemoryKey": "property:cozy-crab",
  "ExternalIds": {
    "GuestyListingId": "abc123",
    "AirbnbListingId": "",
    "VrboListingId":   ""
  },
  "IsActive": true
}
```

| Identifier | Scope | Used For |
|---|---|---|
| `GuestyListingId` | External (Guesty PMS) | Matching inbound webhook reservations |
| `PropertyKey` | Internal (STRAIBot) | Business logic, memory folder, logging |
| `GBrainMemoryKey` | Memory (GBrain) | Scoping all vector memory queries and storage |

External listing IDs from any platform resolve into the same stable `GBrainMemoryKey` — memory is never fragmented by platform.

---

## Example Operational Flow

**Guest message:** `"Can I bring my dog?"`

```
1.  Guesty fires webhook  →  POST /api/webhooks/guesty
    { "event": "reservation.messageReceived", "reservationId": "res_xyz" }

2.  Controller enqueues envelope  →  returns 200 OK immediately

3.  GuestyWebhookProcessor dequeues

4.  GuestyReservationClient  →  GET /reservations/res_xyz
    extracts GuestyListingId: "abc123"

5.  PropertyMappingService.GetByGuestyListingId("abc123")
    →  PropertyKey:     "CozyCrab"
    →  GBrainMemoryKey: "property:cozy-crab"

6.  GBrainClient.QueryAsync("property:cozy-crab", "Can I bring my dog?")
    →  "No pets of any kind. $500 fine if evidence found. No exceptions."

7.  AiGuestMessageDecisionService.DecideAsync(propertyKey, message, context)
    →  OpenAI returns:

{
  "category":           "PetRequest",
  "policyRuleType":     "HardRule",
  "riskLevel":          "Low",
  "confidence":         "High",
  "shouldAutoSend":     true,
  "requiresHostReview": false,
  "shouldNotifyHost":   false,
  "guestResponse":      "Hi! Unfortunately this property does not allow pets of any kind — no exceptions. A $500 fine applies if evidence of a pet is found.",
  "matchedPolicy":      "Pet Policy",
  "reasoningSummary":   "Hard rule match on pet policy. No legal language detected."
}

8.  AiDecisionValidator.ValidateAndCorrect(decision)  →  passes all checks

9.  AutoSendMessaging gates evaluated (Enabled + DryRunMode)

10. GuestyMessageClient.SendMessageAsync(conversationId, guestResponse)
    →  POST /communication/conversations/{id}/send-message
```

---

## Autonomous vs Human Escalation

| Scenario | Auto Send | Host Review | Notify Host |
|---|---|---|---|
| WiFi / amenity question | ✅ Yes | ❌ No | ❌ No |
| Parking / car limit | ✅ Yes | ❌ No | ❌ No |
| Pet policy | ✅ Yes | ❌ No | ❌ No |
| Smoking / vaping | ✅ Yes | ❌ No | ❌ No |
| Late checkout | ✅ Yes | ❌ No | ❌ No |
| Early check-in | ✅ Yes | ✅ Yes | ✅ Yes |
| Party / occupancy | ✅ Yes | ❌ No | ✅ Yes |
| Water leak / lockout / no heat | ✅ Acknowledgement | ✅ Yes | ✅ Yes |
| Service animal / ADA / legal | ✅ Yes | ✅ Yes | ✅ Yes |
| Refund / cancellation | ❌ No | ✅ Yes | ✅ Yes |
| Complaint / damage | ❌ No | ✅ Yes | ✅ Yes |
| AI confidence low | ❌ No | ✅ Yes | ❌ No |

---

## OpenAPI + Scalar

STRAIBot is built **API-first**. All orchestration is inspectable and testable via Scalar before connecting to any live PMS.

```
https://localhost:7048/scalar/v1
```

Scalar enables:
- Full webhook simulation without a live Guesty connection
- Payload inspection and schema validation
- Iterative testing of AI decision output
- API-first development with zero frontend dependency

---

## API Reference

### `POST /api/draft-response`

Manual decision endpoint. Returns full `DraftResponseResult` synchronously. Used for Scalar testing, admin tooling, and prompt tuning.

**Request:**
```json
{
  "propertyName": "CozyCrab",
  "guestMessage": "Can I bring my dog?"
}
```

**Response:**
```json
{
  "propertyName":       "CozyCrab",
  "guestMessage":       "Can I bring my dog?",
  "riskLevel":          "Low",
  "policyRuleType":     "HardRule",
  "category":           "PetRequest",
  "confidence":         "High",
  "shouldAutoSend":     true,
  "requiresHostReview": false,
  "shouldNotifyHost":   false,
  "guestResponse":      "Hi! Unfortunately this property does not allow pets of any kind...",
  "matchedPolicy":      "Pet Policy",
  "reasoningSummary":   "Hard rule match on pet policy.",
  "retrievedContext":   "No pets of any kind. $500 fine...",
  "escalationReason":   null
}
```

**Emergency example:**
```json
{ "propertyName": "TurquoiseBay", "guestMessage": "There is water leaking from the ceiling" }
```
Returns: `riskLevel: Emergency` · `requiresHostReview: true` · `shouldNotifyHost: true`

---

### `POST /api/webhooks/guesty`

Live Guesty webhook receiver. Returns `200 OK` immediately and enqueues for background processing.

**Payload (Guesty fires this):**
```json
{
  "event":         "reservation.messageReceived",
  "reservationId": "res_abc123",
  "data": {
    "_id":            "msg_xyz789",
    "body":           "<p>Can I bring my dog?</p>",
    "direction":      "guest_to_host",
    "conversationId": "conv_456",
    "module":         "email"
  }
}
```

**Response:**
```json
{ "acknowledged": true, "queued": true }
```

---

## Local Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Bun](https://bun.sh) — for GBrain
- WSL2 — recommended on Windows
- OpenAI API key

### 1 — Clone and run

```powershell
git clone https://github.com/chriswarin/STRAIBot.git
cd STRAIBot
dotnet run --project STRAIBot
```

Browser opens to `https://localhost:7048/scalar/v1`

### 2 — WSL2 (Windows)

```powershell
wsl --install
```

### 3 — Install Bun

```sh
curl -fsSL https://bun.sh/install | bash
source ~/.bashrc
```

### 4 — Install GBrain

```sh
bun install -g gbrain
```

### 5 — Set OpenAI API key

```sh
export OPENAI_API_KEY=sk-...
# Add to ~/.bashrc to persist
```

### 6 — Initialize GBrain

> ⚠️ Use the **same embedding model** for init, import, and all queries. Mixing models causes incorrect retrieval.

```sh
GBRAIN_EMBEDDING_MODEL=openai:text-embedding-3-large gbrain init --pglite
```

### 7 — Import property memory

```sh
GBRAIN_EMBEDDING_MODEL=openai:text-embedding-3-large \
gbrain import /mnt/c/Users/chris/source/repos/STRAIBot/memory
```

Import **`memory/` only**. Never point GBrain at `docs/`.

| Path | GBrain import? |
|---|---|
| `memory/**` | ✅ Yes — granular single-topic files |
| `docs/property-memory-dump.md` | ❌ No — human/Copilot reference only |
| `docs/archive/broad-memory/**` | ❌ No — archived multi-topic files, retrieval noise |
| Any other `docs/**` | ❌ No |

Memory is organized as **granular, single-topic files** — one file per policy or amenity per property. This is what makes GBrain retrieve the exact policy instead of a general house-rules document.

> See [`docs/gbrain-memory-structure.md`](docs/gbrain-memory-structure.md) for the full explanation of the import structure and why broad files reduce retrieval precision.

```
memory/
  BlueHorizon/
    policies/         ← HardRule and HostDecision policies, one topic per file
    amenities/        ← Informational facts, one topic per file
  CozyCrab/
    policies/
    amenities/
  TurquoiseBay/
    policies/
    amenities/
```

### 8 — Test retrieval

```sh
GBRAIN_EMBEDDING_MODEL=openai:text-embedding-3-large gbrain search "CozyCrab pet policy"
GBRAIN_EMBEDDING_MODEL=openai:text-embedding-3-large gbrain search "TurquoiseBay parking"
GBRAIN_EMBEDDING_MODEL=openai:text-embedding-3-large gbrain search "BlueHorizon hot tub"
```

### 9 — Configure appsettings.json

```json
{
  "Guesty":   { "BaseUrl": "https://open-api.guesty.com/v1", "AccessToken": "" },
  "Memory":   { "Provider": "Markdown", "GBrainBaseUrl": "http://localhost:8088" },
  "AutoSendMessaging": { "Enabled": false, "DryRunMode": true },
  "PropertyMappings": [
    {
      "PropertyKey":     "CozyCrab",
      "GBrainMemoryKey": "property:cozy-crab",
      "ExternalIds":     { "GuestyListingId": "REPLACE_WITH_REAL_ID" },
      "IsActive":        true
    }
  ]
}
```

> Store real tokens in `appsettings.Development.json` (gitignored) or environment variables — never commit credentials.

---

## Project Structure

```
STRAIBot/
├── Controllers/
│   ├── DraftResponseController.cs      # Manual test + admin endpoint
│   └── GuestyWebhookController.cs      # Guesty webhook receiver
├── Models/
│   ├── AiGuestMessageDecision.cs       # Typed AI output contract
│   ├── DraftResponseResult.cs          # API response shape
│   ├── PropertyMapping.cs              # Property config model
│   ├── PropertyExternalIds.cs          # Per-platform listing IDs
│   ├── Guesty/                         # Guesty API payload models
│   └── Messaging/                      # Outbound message models
├── Services/
│   ├── AiGuestMessageDecisionService   # → OpenAI structured reasoning
│   ├── AiDecisionValidator             # C# policy guardrail
│   ├── DraftResponseService            # Pipeline orchestrator
│   ├── MemoryContextService            # Memory routing (Markdown → GBrain)
│   ├── MarkdownPropertyMemoryService   # Local Markdown fallback
│   ├── GBrainClient                    # GBrain HTTP client
│   ├── Guesty/
│   │   ├── GuestyReservationClient     # Reservation lookup
│   │   └── GuestyMessageClient         # Outbound reply sender
│   ├── Properties/
│   │   ├── PropertyMappingService      # Config-based property lookup
│   │   └── PropertyResolver            # Webhook → property resolution
│   ├── Messaging/
│   │   ├── InMemoryWebhookMessageQueue  # Channel<T> queue
│   │   ├── InMemoryProcessedMessageStore # Dedup tracking
│   │   └── GuestyWebhookProcessor       # BackgroundService pipeline
│   └── Text/
│       └── HtmlMessageCleaner          # Guesty HTML → plain text
├── memory/
│   ├── CozyCrab/
│   ├── BlueHorizon/
│   └── TurquoiseBay/
└── docs/
    └── property-memory-dump.md
```

---

## Current Status

### ✅ Working

- ASP.NET Core 10 API with OpenAPI 3.1 and Scalar
- Guesty webhook receiver with in-process queue
- Reservation → listing → property resolution pipeline
- Multi-platform property mapping (`PropertyKey`, `GBrainMemoryKey`, external IDs)
- Markdown memory retrieval with GBrain interface in place
- Structured `AiGuestMessageDecision` contract
- C# policy validation layer (emergency, legal, approval phrase detection)
- Outbound Guesty reply client with dry-run gate
- Message ID deduplication
- HTML to plain text message cleaning

### 🔄 In Progress

- OpenAI structured JSON decision generation (interface built, placeholder active)
- GBrain live query integration (client built, endpoint contracts defined)
- Live Guesty outbound send validation
- Host escalation notification delivery

### 📋 Planned

- Azure OpenAI support
- Airbnb and VRBO webhook receivers
- Database-backed audit trail and dedup store
- Confidence threshold tuning per property
- Host mobile notifications
- Multi-property admin dashboard
- Retry queue and dead-letter handling
- Policy versioning
- Docker and Azure deployment pipeline

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | ASP.NET Core 10, C# 13 |
| API Docs | OpenAPI 3.1, Scalar |
| Memory | GBrain (vector), Markdown (fallback) |
| AI Reasoning | OpenAI GPT-4o / Azure OpenAI |
| Embeddings | `text-embedding-3-large` |
| PMS Integration | Guesty Open API v1 |
| Queue | `System.Threading.Channels` → Azure Service Bus |
| Local Vector DB | PGLite via GBrain |
| Runtime (GBrain) | Bun, WSL2 |
| Deployment Target | Azure App Service, Docker |
| Persistence (planned) | PostgreSQL / SQL Server via EF Core |

---

## Design Philosophy

**Retrieval before reasoning.**
The AI never operates on a blank context. Every decision is grounded in property-specific knowledge retrieved from GBrain. Hallucination risk is bounded by what retrieval returns.

**Orchestration over raw prompting.**
STRAIBot does not send guest messages to OpenAI and ask it to reply. It sends a structured prompt with context, receives a typed JSON decision, validates it in C#, and then acts. The model is a reasoning component, not an autonomous agent.

**Policy enforcement in the host layer, not the AI layer.**
Emergency overrides, legal escalation rules, and approval phrase detection live in C#. These invariants cannot be bypassed by model misclassification or unexpected output.

**Human-in-the-loop by default.**
`AutoSendMessaging:Enabled` is `false` by default. `DryRunMode` is `true`. Every decision is logged before anything is sent. Autonomy is earned incrementally.

**Memory is operational, not conversational.**
GBrain stores property policies, amenity details, and house rules — not conversation history. The goal is accurate policy recall, not session continuity.

---

## Disclaimer

STRAIBot is an active architecture exploration and MVP. It is not production-ready.

Before production deployment, the following are required:

- Structured observability (tracing, metrics, alerting)
- Durable message queue (Azure Service Bus or equivalent)
- Database-backed audit trail and dedup store
- Webhook signature verification and rate limiting
- AI output monitoring and drift detection
- Policy versioning and change management
- Human approval workflows for edge cases
- Retry logic and dead-letter queue handling
- Load and reliability testing

---

Built by [@chriswarin](https://github.com/chriswarin)

*Orchestration-first. Retrieval-grounded. Policy-aware.*
