# GBrain Troubleshooting — Known Issues

## Issue 1 — PGLite WASM crash on startup

### Symptom

```
PGLite failed to initialize its WASM runtime.
  This is most commonly the macOS 26.3 WASM bug: https://github.com/garrytan/gbrain/issues/223
  Run `gbrain doctor` for a full diagnosis.
  Original error: Aborted(). Build with -sASSERTIONS for more info.
```

GBrain refuses to start. The process exits immediately.

### Root Cause

Three things combine to cause this:

1. **Stale `postmaster.pid`** — if GBrain was killed mid-run (or crashed), PGLite leaves
   a `postmaster.pid` lock file in `~/.gbrain/brain.pglite/`. On the next startup, PGLite
   detects the lock, assumes a previous instance is still running, and aborts before opening
   the database.

2. **Bun 1.3.x regression** — Bun 1.3.x introduced a WASM SIMD compatibility regression
   that causes PGLite to abort during initialization, even with no lock file present.
   **Bun 1.2.14 is the last confirmed stable version for PGLite 0.4.x.**

3. **Corrupted database state** — multiple failed restart cycles can leave the PGLite
   PostgreSQL data directory in an inconsistent state.

### Fix (quick — try this first)

Remove the stale lock file and restart:

```sh
rm -f ~/.gbrain/brain.pglite/postmaster.pid
rm -f ~/.gbrain/brain.pglite/postmaster.opts
```

Then start GBrain normally:
```sh
# From PowerShell:
wsl bash -c "tr -d '\r' < /mnt/c/Users/chris/source/repos/STRAIBot/scripts/start_gbrain.sh > /tmp/sg.sh && bash /tmp/sg.sh"
```

### Fix (full recovery — if quick fix doesn't work)

Run the full recovery script. It backs up the database, pins Bun to 1.2.14, reinitialises
with the correct embedding model, and re-imports all memory files:

```sh
# From PowerShell — set your OpenAI key first:
$env:OPENAI_API_KEY = "sk-..."
wsl bash -c "export OPENAI_API_KEY=$env:OPENAI_API_KEY; tr -d '\r' < /mnt/c/Users/chris/source/repos/STRAIBot/scripts/gbrain_recover.sh > /tmp/recover.sh && bash /tmp/recover.sh"
```

See `scripts/gbrain_recover.sh` for the full annotated script.

---

## Issue 2 — `expected 1536 dimensions, not 1280`

### Symptom

```
Import complete:
  0 pages imported
  86 pages skipped (0 unchanged, 86 errors)
  86 files failed: expected 1536 dimensions, not 1280
```

All files are skipped. Nothing is imported.

### Root Cause

The embedding model used during `gbrain init` must match the model used for `gbrain import`
and every subsequent query. STRAIBot uses `openai:text-embedding-3-large` which produces
**1536-dimensional** vectors.

If `gbrain init` is run **without** the `GBRAIN_EMBEDDING_MODEL` environment variable or
the `--embedding-model` flag, GBrain defaults to a smaller model producing **1280-dimensional**
vectors. The database schema then rejects 1536-dimension imports with a dimension mismatch error.

### Fix

Always initialise, import, and query with the same model:

```sh
export OPENAI_API_KEY=sk-...
export GBRAIN_EMBEDDING_MODEL=openai:text-embedding-3-large

gbrain init --pglite
gbrain import /mnt/c/Users/chris/source/repos/STRAIBot/memory --yes
```

If you already have a mismatched database, delete it and reinitialise:

```sh
rm -rf ~/.gbrain/brain.pglite
GBRAIN_EMBEDDING_MODEL=openai:text-embedding-3-large gbrain init --pglite
GBRAIN_EMBEDDING_MODEL=openai:text-embedding-3-large gbrain import /mnt/c/.../memory --yes
```

Or run the full recovery script (`scripts/gbrain_recover.sh`) which handles this automatically.

---

## Issue 3 — GBrain token expired (no context retrieved, fallback to Markdown)

### Symptom

`/api/memory/test` returns:
```json
{ "gBrainHealthy": false, "fallbackUsed": true, "contextLength": 0 }
```

Or the pipeline retrieves the wrong chunk (e.g. `coffee-kitchen` for a pet policy question).

### Root Cause

GBrain OAuth 2.1 access tokens expire after **1 hour**. When the token expires, `GBrainClient`
receives a 401 and returns empty context. `MemoryContextService` falls back to the Markdown
keyword scorer, which may return an unrelated chunk.

### Fix

Refresh the token by running `scripts/register_gbrain_client.ps1` (token exchange section only)
and updating `Memory:GBrainAccessToken` in `appsettings.json`. Then restart STRAIBot.

```powershell
# Quick token refresh (from PowerShell — replace with your stored client credentials):
$GBRAIN  = "http://172.23.150.171:3131"
$tok = Invoke-RestMethod "$GBRAIN/token" -Method POST `
  -ContentType "application/x-www-form-urlencoded" `
  -Body "grant_type=client_credentials&client_id=YOUR_CLIENT_ID&client_secret=YOUR_CLIENT_SECRET&scope=read"

# Update appsettings.json
$json = Get-Content STRAIBot\appsettings.json -Raw | ConvertFrom-Json
$json.Memory.GBrainAccessToken = $tok.access_token
$json | ConvertTo-Json -Depth 10 | Set-Content STRAIBot\appsettings.json
```

**Long-term fix (planned):** Automatic token refresh in `GBrainClient` using the stored
`client_id` and `client_secret` — tracked as a roadmap item.

---

## Issue 4 — WSL IP changes between sessions

### Symptom

```
Unable to connect to the remote server (http://172.23.150.171:3131)
```

### Root Cause

WSL2 assigns a new IP to the virtual network interface (`eth0`) each time the Windows host
restarts. The IP hardcoded in `Memory:GBrainBaseUrl` becomes stale.

### Fix

Get the current WSL IP and update `appsettings.json`:

```powershell
$wslIp = (wsl bash -c "ip addr show eth0 | grep 'inet ' | awk '{print \$2}' | cut -d/ -f1").Trim()
$json = Get-Content STRAIBot\appsettings.json -Raw | ConvertFrom-Json
$json.Memory.GBrainBaseUrl = "http://${wslIp}:3131"
$json | ConvertTo-Json -Depth 10 | Set-Content STRAIBot\appsettings.json
Write-Host "Updated GBrainBaseUrl to http://${wslIp}:3131"
```

**Long-term fix (planned):** Use `localhost` with a Windows port proxy rule so the IP
never needs updating — tracked as a roadmap item.

---

## Quick Reference

| Problem | First thing to try |
|---|---|
| WASM crash on startup | `rm ~/.gbrain/brain.pglite/postmaster.pid` |
| WASM crash persists | Run `gbrain_recover.sh` (pins Bun to 1.2.14) |
| Dimension mismatch (1280 vs 1536) | Delete DB, reinit with `GBRAIN_EMBEDDING_MODEL=openai:text-embedding-3-large` |
| Token expired / wrong context | Run `register_gbrain_client.ps1`, update `appsettings.json` |
| Cannot connect (WSL IP) | Update `Memory:GBrainBaseUrl` with current WSL IP |
| GBrain healthy but `contextLength: 0` | GBrain running but memory not imported — run `gbrain import` |
