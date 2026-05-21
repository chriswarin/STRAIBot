# STRAIBot

**AI-native operational messaging platform for short-term rentals.**

STRAIBot is a **webhook-driven AI orchestration runtime** — not a chatbot wrapper. It receives inbound PMS events, resolves property context via live semantic memory retrieval (**GBrain MCP**), routes decisions through **OpenAI GPT-4o**, enforces operational policy in C#, and either autonomously replies or escalates to a human — all as a composable, API-first system.

```
PMS Webhook → Property Resolution → GBrain MCP Retrieval → OpenAI GPT-4o Decision → Policy Validation → Send or Escalate
```

> **Beta** · ASP.NET Core 10 · OpenAPI 3.1 · Scalar · GBrain v0.37 · OpenAI GPT-4o · Guesty Open API · C# 13

---

## Why This Exists

Short-term rental operations are fundamentally **repetitive, time-sensitive, policy-bound messaging work**:

- Guests ask the same questions across every reservation
- Policy violations need immediate, accurate responses
- Emergencies demand instant escalation
- Hosts cannot be available 24/7 across 5, 50, or 500 properties

Traditional approaches — templated autoresponders, generic chatbots, offshore VAs — fail at the same boundary: **no operational memory, no policy understanding, no risk classification**.

STRAIBot treats guest messaging as an **event-driven orchestration problem**, not a conversation problem:

| Traditional | STRAIBot |
|---|---|
| Keyword rules | Semantic retrieval + structured reasoning |
| Generic chatbot | Property-scoped vector memory |
| Static FAQ | AI decision with policy validation |
| Manual escalation | Automated risk classification + routing |
| One-size-fits-all | Per-property memory namespaces |

---

## Core Architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│                         INGESTION LAYER                              │
│                                                                      │
│       Guesty Webhook                  Manual API / Scalar UI         │
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
                   │  PropertyMappingService         │
                   │    GuestyListingId              │
                   │    → PropertyKey                │
                   │    → GBrainMemoryKey            │
                   └───────────────┬────────────────┘
                                   │
                   ┌───────────────▼────────────────┐
                   │           MEMORY LAYER          │
                   │                                 │
                   │  GBrainClient  ← LIVE ✅        │
                   │    POST /mcp (MCP SSE)          │
                   │    Bearer auth (OAuth 2.1)      │
                   │    tool: "query"                │
                   │    → chunk_text[] retrieved     │
                   │                                 │
                   │  Fallback: MarkdownMemory ✅    │
                   └───────────────┬────────────────┘
                                   │
                   ┌───────────────▼────────────────┐
                   │        AI REASONING LAYER       │
                   │                                 │
                   │  AiGuestMessageDecisionService  │
                   │    context + guest message      │
                   │    → AiGuestMessageDecision     │
                   │    (OpenAI — next milestone)    │
                   └───────────────┬────────────────┘
                                   │
                   ┌───────────────▼─────────────────┐
                   │      POLICY VALIDATION LAYER ✅  │
                   │  AiDecisionValidator (C#)        │
                   │    emergency override            │
                   │    legal escalation              │
                   │    approval phrase detection     │
                   │    confidence gate               │
                   └──────────┬───────────┬──────────┘
                              │           │
              ┌───────────────▼──┐   ┌───▼──────────────────┐
              │  OUTBOUND SEND   │   │  ESCALATION ROUTING   │
              │  GuestyMessage   │   │  HostNotification     │
              │  DryRun mode ✅  │   │  Service ✅           │
              └──────────────────┘   └───────────────────────┘
```

---

## System Responsibilities

### STRAIBot — Orchestration Runtime

Owns the operational pipeline. Does not generate content, retrieve memory, or make policy decisions — orchestrates the systems that do.

- Receives and acknowledges PMS webhooks with immediate `200 OK`
- Enqueues events for background processing (no inline AI work on the HTTP thread)
- Resolves reservations → listings → internal property keys
- Routes retrieved GBrain context to the AI reasoning layer
- Enforces safety invariants in C# regardless of AI output
- Gates outbound sends via `AutoSendMessaging:Enabled` + `DryRunMode`
- Routes escalations to host notification
- Deduplicates Guesty webhook retries via message ID tracking

### GBrain — Operational Memory Layer ✅ Live

GBrain v0.37 runs locally as an MCP server. STRAIBot connects via OAuth 2.1 client credentials and queries the `query` tool over the MCP SSE protocol.

```
POST /mcp
Authorization: Bearer {token}
Accept: application/json, text/event-stream

{
  "method": "tools/call",
  "params": {
    "name": "query",
    "arguments": {
      "query": "BlueHorizon late checkout cleaners 10am",
      "limit": 3
    }
  }
}
```

Property scoping is achieved by prefixing `{PropertyKey}` into the query. GBrain returns `chunk_text` from the most semantically relevant files in the memory namespace.

**73 granular memory files** across 3 properties — one file per policy or amenity topic.

**GBrain retrieves context. It does not generate guest responses.**

### OpenAI — Structured Reasoning Layer ✅ Live

GPT-4o receives a structured prompt containing the retrieved GBrain context and guest message, and returns a typed `AiGuestMessageDecision` JSON object. The model is instructed to use only the provided property context and never invent policies, exceptions, or approvals.

All OpenAI output is validated by `AiDecisionValidator` (C#) before any message is sent — emergency overrides, legal escalation, and approval phrase detection cannot be bypassed by the model.

Configured via `OpenAI:ApiKey` and `OpenAI:Model` in `appsettings.Development.json` (gitignored).

---

## Verified Test Results

All tested against live GBrain + OpenAI GPT-4o + running API:

| # | Property | Guest Message | riskLevel | policyRuleType | autoSend | notifyHost |
|---|---|---|---|---|---|---|
| ✅ | CozyCrab | "Can I bring my dog?" | Low | HardRule | true | false |
| ✅ | BlueHorizon | "Can I check out at noon?" | Low | HardRule | true | false |
| ✅ | TurquoiseBay | "Is the pool heated?" | Low | Informational | true | false |
| ✅ | TurquoiseBay | "Can we have 16 people?" | Medium | HardRule | true | true |
| ✅ | TurquoiseBay | "Water leaking from ceiling" | Emergency | Emergency | true | true |
| ✅ | BlueHorizon | "Do you allow service animals?" | Medium | HostDecision | false | true |

GBrain retrieval accuracy (confirmed via `/api/memory/test`):

| Query | Expected top chunk | Score |
|---|---|---|
| "BlueHorizon late checkout cleaners 10am" | `bluehorizon/policies/late-checkout-policy` | 0.9999 |
| "CozyCrab no pets fine" | `cozycrab/policies/pet-policy` | exact |
| "TurquoiseBay pool heated April October" | `turquoisebay/policies/pool-policy` | exact |
| "TurquoiseBay 16 people party" | `turquoisebay/policies/party-occupancy-policy` | exact |

---

## Property Memory Model

Each property maps three distinct identifier types:

```json
{
  "PropertyKey":     "CozyCrab",
  "DisplayName":     "Cozy Crab",
  "GBrainMemoryKey": "property:cozy-crab",
  "ExternalIds": {
    "GuestyListingId": "REPLACE_WITH_REAL_ID",
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
| `GBrainMemoryKey` | Memory (GBrain) | Prefixed into every query for property scoping |

---

## Memory Structure

73 granular files across 3 properties — **one file per policy or amenity topic**.

```
memory/
  BlueHorizon/
    policies/    ← pet, late-checkout, parking, smoking, party, MPOA, age, quiet-hours ...
    amenities/   ← ski-access, hot-tub, ev-charger, fireplace, kitchen, bedrooms ...
  CozyCrab/
    policies/    ← pet, late-checkout, party, pool, view, camera, age, cooking ...
    amenities/   ← beds, beach-access, parking, laundry, kitchen, bathroom ...
  TurquoiseBay/
    policies/    ← pet, late-checkout, party-occupancy, pool, canal, landscaper ...
    amenities/   ← beach-access, pool, ev-charger, decks, outdoor-shower, wifi ...
```

> `docs/property-memory-dump.md` — human/Copilot reference only, not imported into GBrain.
> `docs/archive/broad-memory/` — archived pre-granular files, not imported into GBrain.
> See [`docs/gbrain-memory-structure.md`](docs/gbrain-memory-structure.md) for full import rationale.

---

## API Reference

### `GET /api/memory/test`

Diagnostic endpoint. Tests GBrain retrieval for a given property and message. Use in Scalar to verify memory is working before testing the full pipeline.

```
GET /api/memory/test?propertyKey=BlueHorizon&message=late+checkout+cleaners+10am
```

```json
{
  "provider":                  "GBrain",
  "propertyKey":               "BlueHorizon",
  "gBrainMemoryKey":           "property:blue-horizon",
  "gBrainHealthy":             true,
  "fallbackToMarkdownEnabled": true,
  "fallbackUsed":              false,
  "contextLength":             1190,
  "retrievedContext":          "# Blue Horizon — Late Checkout Policy ..."
}
```

---

### `POST /api/draft-response`

Full pipeline — retrieve memory, generate AI decision, validate, return result.

```json
{ "propertyName": "CozyCrab", "guestMessage": "Can I bring my dog?" }
```

```json
{
  "propertyName":       "CozyCrab",
  "riskLevel":          "Low",
  "policyRuleType":     "HardRule",
  "category":           "PetRequest",
  "shouldAutoSend":     true,
  "requiresHostReview": false,
  "shouldNotifyHost":   false,
  "guestResponse":      "Hi there! Unfortunately Cozy Crab does not allow pets of any kind — no exceptions. A $500 fine applies if evidence of a pet is found.",
  "retrievedContext":   "# Cozy Crab — Pet Policy ..."
}
```

**Emergency example:**
```json
{ "propertyName": "TurquoiseBay", "guestMessage": "There is water leaking from the ceiling" }
```
→ `riskLevel: Emergency` · `requiresHostReview: true` · `shouldNotifyHost: true`

---

### `POST /api/webhooks/guesty`

Live webhook receiver. Returns `200 OK` immediately, processes in background.

---

## Local Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Bun **1.2.14**](https://bun.sh) — for GBrain (see note below)
- WSL2 — required for GBrain on Windows
- OpenAI API key — used by both GBrain embeddings and GPT-4o decisions

> **GBrain + Bun compatibility:** Bun 1.3.x breaks PGLite WASM initialization. Use Bun 1.2.14.
> Install it with: `curl -fsSL https://bun.sh/install | bash -s bun-v1.2.14`
> See [`docs/gbrain-troubleshooting.md`](docs/gbrain-troubleshooting.md) if GBrain fails to start.

### 1 — Clone and run

```powershell
git clone https://github.com/chriswarin/STRAIBot.git
cd STRAIBot
dotnet run --project STRAIBot --launch-profile http
```

Scalar UI: `http://localhost:5048/scalar/v1`

### 2 — Install GBrain (WSL2)

```sh
curl -fsSL https://bun.sh/install | bash
source ~/.bashrc
bun install -g gbrain
```

### 3 — Initialize GBrain

> **Critical:** always set `GBRAIN_EMBEDDING_MODEL=openai:text-embedding-3-large` for init, import, and serve.
> If init and import use different models you will get a dimension mismatch error (1536 vs 1280) and all files will fail to import.
> See [`docs/gbrain-troubleshooting.md`](docs/gbrain-troubleshooting.md) — Issue 2.

```sh
export OPENAI_API_KEY=sk-...
export GBRAIN_EMBEDDING_MODEL=openai:text-embedding-3-large
gbrain init --pglite
```

### 4 — Import property memory

```sh
GBRAIN_EMBEDDING_MODEL=openai:text-embedding-3-large \
gbrain import /mnt/c/Users/chris/source/repos/STRAIBot/memory
```

### 5 — Start GBrain HTTP server

Use the recovery script — it handles Bun version, lock files, and prints the admin token:

```powershell
wsl bash -c "tr -d '\r' < /mnt/c/Users/chris/source/repos/STRAIBot/scripts/start_gbrain.sh > /tmp/sg.sh && bash /tmp/sg.sh"
```

Or manually:

```sh
nohup /home/chris/.bun/bin/bun /home/chris/gbrain/src/cli.ts serve \
  --http --port 3131 --enable-dcr --bind 0.0.0.0 \
  >> /tmp/gbrain.log 2>&1 &

cat /tmp/gbrain.log   # copy the Admin Token from the startup banner
```

> If GBrain crashes immediately: see [`docs/gbrain-troubleshooting.md`](docs/gbrain-troubleshooting.md)

### 6 — Register STRAIBot client and get token

```powershell
# Replace {WSL_IP} with your WSL IP (run: wsl hostname -I)
$GBRAIN = "http://{WSL_IP}:3131"

# Register client (one time — persists in PGLite)
$reg = Invoke-RestMethod "$GBRAIN/register" -Method POST `
  -ContentType "application/json" `
  -Body '{"client_name":"straibotlocal","grant_types":["client_credentials"],"token_endpoint_auth_method":"client_secret_post","scope":"read","redirect_uris":["http://localhost/callback"]}'

# Get access token (refresh when expired — TTL: 1 hour)
$tok = Invoke-RestMethod "$GBRAIN/token" -Method POST `
  -ContentType "application/x-www-form-urlencoded" `
  -Body "grant_type=client_credentials&client_id=$($reg.client_id)&client_secret=$($reg.client_secret)&scope=read"

$tok.access_token
```

### 7 — Configure appsettings.json

```json
{
  "Memory": {
    "Provider": "GBrain",
    "FallbackToMarkdown": true,
    "GBrainBaseUrl": "http://{WSL_IP}:3131",
    "GBrainAccessToken": "{access_token_from_step_6}"
  }
}
```

> Store real tokens in `appsettings.Development.json` (gitignored) or environment variables — never commit credentials.

### 8 — Verify retrieval

```powershell
Invoke-RestMethod "http://localhost:5048/api/memory/test?propertyKey=BlueHorizon&message=late+checkout+cleaners+10am" | ConvertTo-Json -Depth 3
Invoke-RestMethod "http://localhost:5048/api/memory/test?propertyKey=CozyCrab&message=no+pets+fine" | ConvertTo-Json -Depth 3
Invoke-RestMethod "http://localhost:5048/api/memory/test?propertyKey=TurquoiseBay&message=pool+heated+April+October" | ConvertTo-Json -Depth 3
Invoke-RestMethod "http://localhost:5048/api/memory/test?propertyKey=TurquoiseBay&message=16+people+party" | ConvertTo-Json -Depth 3
```

### 9 — Test full pipeline

```powershell
# Pet request — HardRule
Invoke-RestMethod "http://localhost:5048/api/draft-response" -Method POST -ContentType "application/json" -Body '{"propertyName":"CozyCrab","guestMessage":"Can I bring my dog?"}' | ConvertTo-Json

# Emergency — notify host
Invoke-RestMethod "http://localhost:5048/api/draft-response" -Method POST -ContentType "application/json" -Body '{"propertyName":"TurquoiseBay","guestMessage":"There is water leaking from the ceiling"}' | ConvertTo-Json

# Service animal — escalate to host
Invoke-RestMethod "http://localhost:5048/api/draft-response" -Method POST -ContentType "application/json" -Body '{"propertyName":"BlueHorizon","guestMessage":"Do you allow service animals?"}' | ConvertTo-Json
```

---

## Project Structure

```
STRAIBot/
├── Controllers/
│   ├── DraftResponseController.cs      # Full pipeline endpoint
│   ├── GuestyWebhookController.cs      # Guesty webhook receiver
│   └── MemoryTestController.cs         # GBrain retrieval diagnostic
├── Models/
│   ├── AiGuestMessageDecision.cs       # Typed AI output contract
│   ├── DraftResponseResult.cs          # API response shape
│   ├── PropertyMapping.cs              # Property config + GBrainMemoryKey
│   └── ...
├── Services/
│   ├── GBrain/
│   │   ├── IGBrainClient.cs            # MCP client interface
│   │   └── GBrainClient.cs            # OAuth 2.1 + SSE + query tool
│   ├── Memory/
│   │   ├── IMemoryContextService.cs    # Unified memory interface
│   │   └── MemoryContextService.cs    # GBrain → Markdown routing
│   ├── OpenAI/
│   │   ├── IOpenAiDecisionClient.cs    # OpenAI client interface
│   │   └── OpenAiDecisionClient.cs    # GPT-4o structured JSON call
│   ├── AiGuestMessageDecisionService   # OpenAI first, placeholder fallback
│   ├── AiDecisionValidator             # C# safety guardrail — always runs
│   ├── DraftResponseService            # Pipeline orchestrator
│   ├── Guesty/                         # Reservation + message clients
│   ├── Properties/                     # Property mapping + resolution
│   ├── Messaging/                      # Queue + background processor
│   └── Text/                           # HTML cleaner
├── scripts/
│   ├── start_gbrain.sh                 # Start GBrain MCP server
│   ├── register_gbrain_client.ps1      # OAuth client registration + token
│   ├── gbrain_recover.sh               # Full recovery (Bun pin + DB reinit)
│   └── fix_gbrain.py                   # Reference: dual-namespace fix
├── memory/
│   ├── BlueHorizon/policies/ + amenities/
│   ├── CozyCrab/policies/ + amenities/
│   └── TurquoiseBay/policies/ + amenities/
└── docs/
    ├── gbrain-troubleshooting.md       # WASM crash, dim mismatch, token expiry
    ├── gbrain-memory-structure.md      # Memory architecture + import rules
    ├── property-memory-dump.md         # Human/Copilot reference only
    └── archive/broad-memory/           # Archived pre-granular files
```

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | ASP.NET Core 10, C# 13 |
| API Docs | OpenAPI 3.1, Scalar |
| Memory | GBrain v0.37 (vector MCP), Markdown (fallback) |
| Embeddings | `text-embedding-3-large` (OpenAI) — 1536 dimensions |
| MCP Protocol | JSON-RPC 2.0 over HTTP SSE, OAuth 2.1 |
| AI Reasoning | OpenAI GPT-4o (live), keyword placeholder (fallback) |
| PMS Integration | Guesty Open API v1 |
| Queue | `System.Threading.Channels` → Azure Service Bus (planned) |
| Local Vector DB | PGLite via GBrain |
| Runtime (GBrain) | Bun, WSL2 |
| Deployment Target | Azure App Service / Docker (planned) |

---

## Design Philosophy

**Retrieval before reasoning.**
The AI never operates on a blank context. Every decision is grounded in retrieved property-specific knowledge from GBrain. Hallucination risk is bounded by what retrieval returns.

**Orchestration over raw prompting.**
STRAIBot does not send guest messages to an LLM and ask it to reply. It sends a structured prompt with retrieved context, receives a typed JSON decision, validates it in C#, then acts.

**Policy enforcement in the host layer, not the AI layer.**
Emergency overrides, legal escalation rules, and approval phrase detection live in C#. These invariants cannot be bypassed by model misclassification.

**Human-in-the-loop by default.**
`AutoSendMessaging:Enabled` is `false`. `DryRunMode` is `true`. Every decision is logged before anything is sent. Autonomy is earned incrementally.

**Memory is operational, not conversational.**
GBrain stores property policies and amenity facts — not conversation history. The goal is accurate policy recall, not session continuity.

---

## Roadmap

### ✅ Beta Complete
- ASP.NET Core 10 API with OpenAPI + Scalar
- GBrain MCP integration (OAuth 2.1, SSE, `query` tool, 86 memory files)
- OpenAI GPT-4o structured JSON decision generation (live)
- Typed `AiGuestMessageDecision` contract with C# safety validator
- Emergency override, legal escalation, approval phrase detection
- Guesty webhook receiver + background processing queue
- Property mapping (PropertyKey, GBrainMemoryKey, platform IDs)
- Markdown fallback when GBrain unavailable
- Deduplication store, HTML message cleaning
- Memory test diagnostic endpoint (`GET /api/memory/test`)
- GBrain recovery scripts + troubleshooting docs

### 🔜 Next
- Guesty outbound reply (live send with dry-run gate)
- Automatic GBrain OAuth token refresh
- WSL IP auto-detection (no manual appsettings update on reboot)

### 📋 Planned
- Azure OpenAI support
- Airbnb + VRBO webhook receivers
- Database audit trail (EF Core + PostgreSQL)
- Host mobile notifications (SMS/push)
- Multi-property admin dashboard
- Docker + Azure deployment pipeline
- Retry queue + dead-letter handling

---

## Disclaimer

STRAIBot is a **beta prototype** with live OpenAI GPT-4o and GBrain MCP integration. Not production-ready.

Production requirements include: structured observability, durable message queue, database audit trail, webhook signature verification, rate limiting, AI output monitoring, policy versioning, human approval workflows, and load testing.

---

Built by [@chriswarin](https://github.com/chriswarin)

*Orchestration-first. Retrieval-grounded. Policy-aware.*
