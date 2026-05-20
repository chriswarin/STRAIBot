# Broad Memory Archive

These files are archived broad memory files retained for **human reference only**.

## Do NOT import these files into GBrain

These files contain multiple topics per file (e.g. a full house-rules file covering pets,
smoking, parking, quiet hours, cooking, and checkout all in one document). When GBrain
embeds and retrieves these files, it returns a general house-rules chunk instead of the
exact policy the guest asked about. This reduces retrieval precision and can cause the
AI to respond with the wrong policy level or miss key details like exact fine amounts.

## Active GBrain import source

```
memory/**
```

The `memory/` directory contains granular, single-topic files — one file per policy or
amenity per property. These are the files GBrain should embed and retrieve from.

## Files in this archive

```
docs/archive/broad-memory/
  BlueHorizon/
    house-rules.md          ← multi-topic: all house rules in one file
    common-responses.md     ← multi-topic: general responses
    amenities.md            ← multi-topic: all amenities in one file
    hot-tub.md              ← pre-granular version (superseded by amenities/hot-tub.md)
  CozyCrab/
    house-rules.md
    common-responses.md
    amenities.md
    parking.md              ← superseded by amenities/parking.md
  TurquoiseBay/
    house-rules.md
    common-responses.md
    amenities.md
    beach-items.md
```

## Granular equivalents

Each archived file has been replaced by targeted files under `memory/`:

| Archived file | Granular replacements |
|---|---|
| `*/house-rules.md` | `*/policies/pet-policy.md`, `*/policies/smoking-policy.md`, `*/policies/parking-policy.md`, etc. |
| `*/amenities.md` | `*/amenities/overview.md`, `*/amenities/hot-tub.md`, `*/amenities/coffee-kitchen.md`, etc. |
| `*/common-responses.md` | Individual `## Suggested Response` blocks in each granular file |
| `BlueHorizon/hot-tub.md` | `BlueHorizon/amenities/hot-tub.md` |
| `CozyCrab/parking.md` | `CozyCrab/amenities/parking.md` |
| `TurquoiseBay/beach-items.md` | `TurquoiseBay/amenities/beach-access.md` |
