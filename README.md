<div align="center">

<img src="https://img.shields.io/badge/Platform-.NET%2010-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
<img src="https://img.shields.io/badge/AI-Ready-FF6B35?style=for-the-badge&logo=openai&logoColor=white" />
<img src="https://img.shields.io/badge/GBrain-Integrated-00C896?style=for-the-badge&logoColor=white" />
<img src="https://img.shields.io/badge/Guesty-Connected-1A73E8?style=for-the-badge&logoColor=white" />
<img src="https://img.shields.io/badge/Status-Active%20Development-brightgreen?style=for-the-badge" />

# 🏖️ STRAIBot

### *The AI-Powered Guest Communication Engine for Short-Term Rentals*

**Respond faster. Escalate smarter. Scale without hiring.**

[Architecture](#-architecture) · [Features](#-features) · [Roadmap](#-roadmap) · [Tech Stack](#-tech-stack) · [Getting Started](#-getting-started) · [Investor Vision](#-investor-vision)

---

</div>

## 🚀 What Is STRAIBot?

STRAIBot is an **AI-native guest communication platform** built for short-term rental hosts and property managers. It sits between your property management system and your guests — automatically classifying inbound messages, retrieving property-specific context, generating policy-grounded responses, and deciding in real time whether to auto-reply or escalate to a human.

> **Receive a guest message → classify it → retrieve memory → generate a response → send or escalate.**
> All in under a second. All grounded in your actual property policies.

No generic chatbot. No hallucinated amenities. No wrong answers about checkout times.

---

## 💡 The Problem

Short-term rental hosts and property managers spend **hours every day** responding to the same guest questions:

- *"Can I bring my dog?"*
- *"Is late checkout available?"*
- *"Is the pool heated?"*
- *"There's water leaking from the ceiling."*

For operators managing 5, 50, or 500 listings, this is unsustainable. Hiring guest communication staff is expensive and doesn't scale. Generic chatbots give wrong answers and destroy trust.

**STRAIBot solves this by combining AI decision-making with property-specific memory — so every response is accurate, policy-grounded, and on-brand.**

---

## ✨ Features

### 🧠 AI-Driven Decision Engine
Every inbound message produces a structured `AiGuestMessageDecision` JSON contract:
- **Risk classification** — Emergency / High / Medium / Low
- **Policy rule type** — HardRule / HostDecision / Informational / Emergency
- **Auto-send gate** — should the response go now, or wait for human review?
- **Confidence scoring** — Low / Medium / High
- **Escalation reasoning** — why was this flagged for the host?

### 🏠 Property Memory (Markdown → GBrain)
STRAIBot retrieves property-specific context before generating any response. Today that's structured Markdown files. Tomorrow it's **GBrain** — a vector memory system that stores and retrieves property knowledge at semantic depth.

```
memory/
  CozyCrab/
    amenities.md
    house-rules.md
    common-responses.md
    parking.md
  BlueHorizon/
    amenities.md
    house-rules.md
    hot-tub.md
    common-responses.md
  TurquoiseBay/
    amenities.md
    house-rules.md
    beach-items.md
    common-responses.md
```

### 🔒 Safety Validator — C# as the Guardrail Layer
The `AiDecisionValidator` runs after every AI decision and enforces hard invariants:
- Never approve pets, parties, extra guests, late checkout, or service animal requests automatically
- Always force Emergency classification for fire/smoke/gas/leak/lockout messages
- Block auto-send if confidence is low or response is empty
- Log every correction for audit trail

### ⚡ Full Duplex Messaging
```
Guesty Webhook  →  STRAIBot receives
STRAIBot thinks →  AI decision generated
STRAIBot sends  →  Reply posted to Guesty conversation thread
```

### 🔄 Guesty PMS Integration
- Webhook receiver at `POST /api/webhooks/guesty`
- Reservation lookup via Guesty Open API to resolve property from listing ID
- Outbound replies via `POST /communication/conversations/{id}/send-message`
- Duplicate protection via message ID deduplication
- Direction filtering — never processes host-sent messages

### 🧭 Multi-Platform Property Mapping
One property. Multiple platforms. One internal key.
```json
{
  "PropertyKey": "CozyCrab",
  "GBrainMemoryKey": "property:cozy-crab",
  "ExternalIds": {
    "GuestyListingId": "...",
    "AirbnbListingId": "...",
    "VrboListingId": "..."
  }
}
```

### 🛡️ Controlled Auto-Send
```json
"AutoSendMessaging": {
  "Enabled": false,
  "DryRunMode": true
}
```
Three modes: **off** (log only) → **dry run** (simulate, no API call) → **live** (real send). You decide when to flip the switch.

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        INBOUND                              │
│                                                             │
│   Guesty Webhook ──→ POST /api/webhooks/guesty              │
│   Swagger/Admin  ──→ POST /api/draft-response               │
└────────────────────────────┬────────────────────────────────┘
                             │
                    ┌────────▼────────┐
                    │  Webhook Queue  │  (Channel<T>, in-memory)
                    │  + Dedup Store  │  → future: Redis / SQL
                    └────────┬────────┘
                             │
              ┌──────────────▼──────────────┐
              │    GuestyWebhookProcessor   │
              │       (BackgroundService)   │
              └──────────────┬──────────────┘
                             │
         ┌───────────────────▼───────────────────┐
         │           DraftResponseService         │
         │              (Orchestrator)            │
         └───┬──────────────┬────────────────┬───┘
             │              │                │
    ┌────────▼───┐  ┌───────▼──────┐  ┌─────▼──────────┐
    │  Memory    │  │  AI Decision │  │   Validator    │
    │  Context   │  │   Service    │  │  (C# Guardrail)│
    │  Service   │  │              │  │                │
    └────────┬───┘  └───────┬──────┘  └─────┬──────────┘
             │              │                │
    ┌────────▼───┐  ┌───────▼──────┐         │
    │  Markdown  │  │  → OpenAI    │         │
    │  (today)   │  │    Azure OAI │         │
    │  GBrain    │  │  (tomorrow)  │         │
    │  (next)    │  └──────────────┘         │
    └────────────┘                           │
                                             │
                    ┌────────────────────────▼───┐
                    │       Host Notification    │
                    │    + Guesty Reply Send     │
                    └────────────────────────────┘
```

---

## 🗺️ Roadmap

### ✅ Completed
- [x] ASP.NET Core 10 API with Scalar/OpenAPI docs
- [x] Markdown-based property memory (3 properties)
- [x] AI decision contract (`AiGuestMessageDecision`)
- [x] C# safety validator (hard rule enforcement)
- [x] Guesty webhook receiver
- [x] Full duplex messaging (receive + reply)
- [x] Property resolver (Guesty listing → internal key)
- [x] Multi-platform property mapping model
- [x] Duplicate message deduplication
- [x] HTML message body cleaning
- [x] Background processor with controlled auto-send gate
- [x] Dry-run mode for safe testing

### 🔜 In Progress / Next
- [ ] **GBrain integration** — vector memory retrieval per property using `GBrainMemoryKey`
- [ ] **OpenAI / Azure OpenAI** — replace local placeholder with real AI decision generation
- [ ] **Guesty OAuth** — token refresh and scoped access
- [ ] **Database persistence** — message audit trail, processed message store, analytics
- [ ] **Multi-property dashboard** — admin UI for reviewing drafts and approvals
- [ ] **Host escalation notifications** — SMS/email/push on high-risk messages
- [ ] **Confidence threshold tuning** — per-property auto-send confidence floors
- [ ] **Airbnb + VRBO webhook receivers**
- [ ] **Docker containerization**
- [ ] **Azure deployment pipeline**
- [ ] **Retry queue + dead-letter queue** for failed sends

---

## 🧬 GBrain — The Memory Layer

GBrain is STRAIBot's **vector memory system**. Each property has a dedicated memory namespace (`GBrainMemoryKey`) where all property-specific knowledge is stored as semantic embeddings.

When a guest sends a message, STRAIBot:
1. Identifies the property via `PropertyKey`
2. Queries GBrain using `GBrainMemoryKey` as the memory scope
3. Retrieves the most semantically relevant context for that specific question
4. Passes the retrieved context to the AI decision engine

This means the AI never needs to see all property information — it only sees the **relevant slice**, making responses faster, cheaper, and more accurate.

```
GBrain Query:
{
  "propertyKey":     "CozyCrab",
  "gBrainMemoryKey": "property:cozy-crab",
  "guestMessage":    "Is the pool heated?",
  "listingId":       "guesty-abc123"
}

GBrain Response:
"The outdoor pool is seasonal and unheated.
 Usually open mid-May through mid-October, weather dependent."
```

**No hallucinations. No invented amenities. No wrong answers.**

---

## 🤖 OpenAI Integration — Coming Next

The `IAiGuestMessageDecisionService` interface is already built. The local placeholder will be replaced by a single OpenAI API call that sends:

```
System prompt: You are an AI assistant for a short-term rental...
               Return only valid JSON matching AiGuestMessageDecision.
               Never approve pets, parties, late checkout, or refunds.

User prompt:   PropertyName: CozyCrab
               GuestMessage: Can I bring my dog?
               RetrievedContext: [from GBrain]
```

And receives back a structured `AiGuestMessageDecision` JSON — validated by the C# safety layer before any response is sent.

---

## 🛠️ Tech Stack

| Layer | Technology |
|---|---|
| **API** | ASP.NET Core 10, C# 13 |
| **API Docs** | Scalar / OpenAPI |
| **Memory (now)** | Structured Markdown files |
| **Memory (next)** | GBrain vector memory |
| **AI (next)** | OpenAI GPT-4o / Azure OpenAI |
| **PMS** | Guesty Open API v1 |
| **Messaging** | `System.Threading.Channels` (in-process) → Azure Service Bus (future) |
| **Auth** | Bearer token (Guesty), OpenAI API key |
| **Deployment target** | Azure App Service / Docker |
| **Persistence (next)** | SQL Server / PostgreSQL via EF Core |

---

## 📦 Getting Started

### Prerequisites
- .NET 10 SDK
- Visual Studio 2022+ or VS Code
- Guesty account (optional — runs fully without it in local mode)

### Run locally

```bash
git clone https://github.com/chriswarin/STRAIBot.git
cd STRAIBot
dotnet run --project STRAIBot
```

Browser opens automatically to **https://localhost:7048/scalar/v1**

### Test in Scalar

**Pet question (HardRule — auto-respond, no host):**
```json
POST /api/draft-response
{
  "propertyName": "CozyCrab",
  "guestMessage": "Can I bring my dog?"
}
```

**Emergency (immediate host notification):**
```json
POST /api/draft-response
{
  "propertyName": "TurquoiseBay",
  "guestMessage": "There is water leaking from the ceiling"
}
```

**Late checkout (HardRule — firm no):**
```json
POST /api/draft-response
{
  "propertyName": "BlueHorizon",
  "guestMessage": "Can we check out at noon?"
}
```

### Configure for live Guesty

In `appsettings.json`, replace the placeholder listing IDs:

```json
"Guesty": {
  "BaseUrl": "https://open-api.guesty.com/v1",
  "AccessToken": "your-token-here"
},
"PropertyMappings": [
  {
    "PropertyKey": "CozyCrab",
    "GBrainMemoryKey": "property:cozy-crab",
    "ExternalIds": {
      "GuestyListingId": "your-real-guesty-listing-id"
    },
    "IsActive": true
  }
]
```

Set `AutoSendMessaging:Enabled = true` and `DryRunMode = false` when ready for live sends.

---

## 📁 Project Structure

```
STRAIBot/
├── Controllers/
│   ├── DraftResponseController.cs     # Manual/Swagger testing endpoint
│   └── GuestyWebhookController.cs     # Live Guesty webhook receiver
├── Models/
│   ├── AiGuestMessageDecision.cs      # AI decision contract
│   ├── DraftResponseResult.cs         # API response shape
│   ├── PropertyMapping.cs             # Property config model
│   ├── PropertyExternalIds.cs         # Per-platform listing IDs
│   ├── Guesty/                        # Guesty API models
│   └── Messaging/                     # Outbound message models
├── Services/
│   ├── AiGuestMessageDecisionService  # AI decision (→ OpenAI)
│   ├── AiDecisionValidator            # C# safety guardrail
│   ├── DraftResponseService           # Pipeline orchestrator
│   ├── MemoryContextService           # Memory routing (Markdown → GBrain)
│   ├── MarkdownPropertyMemoryService  # Local markdown lookup
│   ├── GBrainClient                   # GBrain HTTP client (ready)
│   ├── Guesty/                        # Guesty API clients
│   ├── Properties/                    # Property resolution
│   ├── Messaging/                     # Queue + background processor
│   └── Text/                          # HTML cleaner
├── memory/
│   ├── BlueHorizon/                   # Property knowledge files
│   ├── CozyCrab/
│   └── TurquoiseBay/
└── docs/
    └── property-memory-dump.md        # Full property reference
```

---

## 💼 Investor Vision

The short-term rental industry is a **$100B+ global market** with millions of individual hosts and thousands of property management companies — nearly all of them under-resourced on guest communications.

**STRAIBot's opportunity:**

| Segment | Problem | STRAIBot Solution |
|---|---|---|
| Independent hosts (1–5 properties) | No staff, constant interruptions | Fully automated responses, zero hiring |
| Mid-size operators (5–50 properties) | Staff burnout, inconsistent responses | AI handles 80%+, humans handle edge cases |
| Large PMCs (50–500+ properties) | Scale impossible without headcount | White-label AI layer, API-first integration |

### Why now?
- **GPT-4o** makes accurate, property-grounded responses possible at <$0.01/message
- **Guesty, Hostaway, Lodgify** all have open APIs — integration is viable today
- **Vector memory** (GBrain) solves the hallucination problem that killed earlier chatbot attempts
- **Guests expect instant replies** — 5-minute response windows are now the norm on Airbnb

### Architecture advantages
- **AI-agnostic** — one interface swap connects OpenAI, Azure OpenAI, Anthropic, or any future model
- **PMS-agnostic** — property mapping model supports Guesty, Airbnb, VRBO, and any future platform
- **Memory-agnostic** — Markdown today, GBrain tomorrow, any vector DB the day after
- **No lock-in** — C# safety validator ensures brand safety regardless of which AI is underneath

### Revenue model (planned)
- SaaS per-property monthly subscription
- Usage-based pricing for high-volume operators
- White-label licensing for large PMCs and channel managers

---

## 🤝 Contributing

STRAIBot is actively developed. If you're interested in contributing to the GBrain integration, OpenAI decision layer, or property management integrations — open an issue or reach out directly.

---

## 📄 License

MIT License — see [LICENSE](LICENSE) for details.

---

<div align="center">

**Built with ❤️ for the short-term rental industry**

*STRAIBot — where AI meets hospitality*

</div>
