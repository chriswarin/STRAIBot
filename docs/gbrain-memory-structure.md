# GBrain Memory Structure

## Why property-memory-dump.md is NOT the GBrain import source

`docs/property-memory-dump.md` is a **human and Copilot reference file**. It contains all property knowledge in one place for easy reading, debugging, and AI assistant context.

It is intentionally **not** used as the GBrain import source because:

- A large dump file causes GBrain to return entire property dumps as a single retrieval chunk
- The embedding covers too many topics at once, diluting semantic relevance
- When a guest asks "is the pool heated?" GBrain should return the pool policy chunk only — not the entire property document
- A monolithic file cannot rank one policy above another for a specific guest query

---

## Why memory/** files are granular

Each file under `memory/` covers **one topic for one property**. This gives GBrain the smallest possible semantic unit to embed and retrieve.

When a guest asks about late checkout, GBrain retrieves `late-checkout-policy.md` — a 20-line file focused entirely on checkout time, the $200 fee, and the "cleaners arrive at 10am" fact. Nothing else.

When a guest asks about the pool, GBrain retrieves `pool-policy.md` — which includes the unheated/saltwater/April–October facts, the Friday servicing note, and the pool rules. Exactly what is needed. Nothing more.

This retrieval precision is what prevents the AI from hallucinating or mixing up policies between properties.

---

## File structure

```
memory/
  {PropertyKey}/
    policies/
      {policy-topic}.md      ← HardRule or HostDecision policies
    amenities/
      {amenity-topic}.md     ← Informational facts
```

Each file follows this template:

```markdown
# {Display Name} — {Topic}

PropertyKey: {PropertyKey}
DisplayName: {DisplayName}
GBrainMemoryKey: property:{kebab-slug}
Topic: {Topic}
PolicyRuleType: HardRule | Informational | HostDecision | Emergency
AutoRespond: true | false
RequiresHostReview: true | false
ShouldNotifyHost: true | false

## Policy or Fact
## Guest Response Guidance
## Suggested Response
## Escalation Notes
```

The metadata block at the top is included in the embedding so GBrain can match on
`PropertyKey`, `GBrainMemoryKey`, and `Topic` as part of the semantic retrieval context.

---

## GBrain query structure

When STRAIBot queries GBrain, every query should include:

```json
{
  "propertyKey":     "TurquoiseBay",
  "gBrainMemoryKey": "property:turquoise-bay",
  "guestMessage":    "Is the pool heated?",
  "topK":            5
}
```

- `gBrainMemoryKey` scopes retrieval to only this property's memory namespace
- `guestMessage` drives semantic similarity matching
- `topK` limits to the most relevant chunks only
- `propertyKey` can be used for additional post-retrieval filtering

**Never query GBrain without `gBrainMemoryKey`.** Without it, results may pull from
the wrong property's memory and produce incorrect guest responses.

---

## Memory namespace mapping

| PropertyKey   | GBrainMemoryKey           | Memory Folder            |
|---------------|---------------------------|--------------------------|
| BlueHorizon   | property:blue-horizon     | memory/BlueHorizon/      |
| CozyCrab      | property:cozy-crab        | memory/CozyCrab/         |
| TurquoiseBay  | property:turquoise-bay    | memory/TurquoiseBay/     |

---

## Importing into GBrain

After adding or updating files under `memory/`, re-import with the **same embedding model**
used during `gbrain init`. Mixing models produces incorrect retrieval.

```sh
GBRAIN_EMBEDDING_MODEL=openai:text-embedding-3-large \
gbrain import /mnt/c/Users/chris/source/repos/STRAIBot/memory
```

To verify retrieval is working correctly after import, test the four key scenarios:

```sh
# Should return BlueHorizon late-checkout-policy.md
GBRAIN_EMBEDDING_MODEL=openai:text-embedding-3-large \
gbrain search "BlueHorizon late checkout cleaners 10am"

# Should return CozyCrab pet-policy.md
GBRAIN_EMBEDDING_MODEL=openai:text-embedding-3-large \
gbrain search "CozyCrab no pets fine"

# Should return TurquoiseBay pool-policy.md
GBRAIN_EMBEDDING_MODEL=openai:text-embedding-3-large \
gbrain search "TurquoiseBay pool heated April October"

# Should return TurquoiseBay party-occupancy-policy.md
GBRAIN_EMBEDDING_MODEL=openai:text-embedding-3-large \
gbrain search "TurquoiseBay 16 people party"
```

---

## Host should verify markers

Some files contain `Host should verify` in place of facts that were not provided in the
source data. These are intentional placeholders — the AI will not invent missing information.

Files with host-verify items:
- `TurquoiseBay/amenities/coffee-kitchen.md` — coffee machine type
- `TurquoiseBay/amenities/ev-charger.md` — exact charger voltage and speed
- `TurquoiseBay/amenities/laundry.md` — free vs paid washer/dryer
- `TurquoiseBay/amenities/safety-security.md` — smoke/CO/extinguisher confirmation

Update these files with confirmed data before going live and re-import into GBrain.
