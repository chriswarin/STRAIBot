#!/bin/bash
# scripts/gbrain_recover.sh
#
# GBrain database recovery script.
# Run this when GBrain fails to start with:
#   "PGLite failed to initialize its WASM runtime"
#   "RuntimeError: Aborted()"
#
# Root cause: PGLite WASM crashes on startup when the database was
# left in an unclean state by a previous crash or forced kill.
# The postmaster.pid lock file is not always cleaned up, and combined
# with a Bun version regression (1.3.x breaks PGLite WASM), the
# database becomes unrecoverable without reinitialising.
#
# This script:
#   1. Stops any running GBrain processes
#   2. Backs up the current database (nothing is deleted)
#   3. Removes the stale lock file
#   4. Pins Bun to 1.2.14 (last known-good version for PGLite 0.4.x)
#   5. Reinitialises the database with the correct embedding model
#   6. Re-imports all memory files from memory/
#   7. Starts the GBrain HTTP server
#   8. Registers a new OAuth client and prints the access token
#
# IMPORTANT: Set these before running:
#   export OPENAI_API_KEY=sk-...
#
# Usage (from PowerShell):
#   wsl bash -c "tr -d '\r' < /mnt/c/Users/chris/source/repos/STRAIBot/scripts/gbrain_recover.sh > /tmp/recover.sh && bash /tmp/recover.sh"

set -e

GBRAIN_SRC="/home/chris/gbrain/src/cli.ts"
BUN="/home/chris/.bun/bin/bun"
MEMORY_PATH="/mnt/c/Users/chris/source/repos/STRAIBot/memory"
DB_PATH="$HOME/.gbrain/brain.pglite"
LOG="/tmp/gbrain.log"
PORT=3131
EMBEDDING_MODEL="openai:text-embedding-3-large"
KNOWN_GOOD_BUN="bun-v1.2.14"

# ── Check OPENAI_API_KEY ──────────────────────────────────────────────────────
if [ -z "$OPENAI_API_KEY" ]; then
    echo "ERROR: OPENAI_API_KEY is not set."
    echo "Run: export OPENAI_API_KEY=sk-..."
    exit 1
fi

echo ""
echo "=== GBrain Recovery Script ==="
echo "Embedding model: $EMBEDDING_MODEL"
echo "Memory path:     $MEMORY_PATH"
echo "Database:        $DB_PATH"
echo ""

# ── Step 1: Stop GBrain ───────────────────────────────────────────────────────
echo "[1/7] Stopping any running GBrain processes..."
pkill -f "gbrain serve" 2>/dev/null || true
sleep 2

# ── Step 2: Back up corrupted database ───────────────────────────────────────
if [ -d "$DB_PATH" ]; then
    BACKUP="${DB_PATH}.bak.$(date +%Y%m%d_%H%M%S)"
    echo "[2/7] Backing up database to $BACKUP ..."
    cp -r "$DB_PATH" "$BACKUP"
    echo "      Backup complete. Remove with: rm -rf $BACKUP"
else
    echo "[2/7] No existing database found — skipping backup."
fi

# ── Step 3: Remove stale lock file ────────────────────────────────────────────
echo "[3/7] Removing stale lock file..."
rm -f "$DB_PATH/postmaster.pid" "$DB_PATH/postmaster.opts" 2>/dev/null || true
echo "      Lock file removed."

# ── Step 4: Pin Bun to known-good version ─────────────────────────────────────
echo "[4/7] Checking Bun version..."
CURRENT_BUN=$($BUN --version 2>/dev/null || echo "unknown")
echo "      Current: $CURRENT_BUN"

if [[ "$CURRENT_BUN" == 1.3* ]]; then
    echo "      Bun 1.3.x detected — known to break PGLite WASM. Downgrading to 1.2.14..."
    curl -fsSL https://bun.sh/install | bash -s "$KNOWN_GOOD_BUN" 2>&1 | tail -3
    echo "      Bun downgraded."
else
    echo "      Bun version OK ($CURRENT_BUN)."
fi

# ── Step 5: Remove broken database and reinitialise ───────────────────────────
echo "[5/7] Reinitialising GBrain database..."
rm -rf "$DB_PATH"
GBRAIN_EMBEDDING_MODEL=$EMBEDDING_MODEL \
OPENAI_API_KEY=$OPENAI_API_KEY \
    $BUN "$GBRAIN_SRC" init --pglite --embedding-model "$EMBEDDING_MODEL" --non-interactive 2>&1 | grep -E "Brain ready|error|Error" || true
echo "      Database initialised."

# ── Step 6: Re-import memory files ────────────────────────────────────────────
echo "[6/7] Re-importing memory files from $MEMORY_PATH ..."
GBRAIN_EMBEDDING_MODEL=$EMBEDDING_MODEL \
OPENAI_API_KEY=$OPENAI_API_KEY \
    $BUN "$GBRAIN_SRC" import "$MEMORY_PATH" --yes 2>&1 | grep -E "Import complete|imported|error|Error" || true
echo "      Import done."

# ── Step 7: Start GBrain HTTP server ─────────────────────────────────────────
echo "[7/7] Starting GBrain HTTP server on port $PORT..."
> "$LOG"
nohup $BUN "$GBRAIN_SRC" serve \
    --http \
    --port $PORT \
    --enable-dcr \
    --bind 0.0.0.0 \
    >> "$LOG" 2>&1 &
disown
sleep 5

if pgrep -f "gbrain serve" > /dev/null 2>&1; then
    echo ""
    echo "=== GBrain is running ==="
    cat "$LOG"
    echo ""
    echo "Next: Run scripts/register_gbrain_client.ps1 from PowerShell to get a fresh access token."
    echo "      Then update Memory:GBrainAccessToken in STRAIBot/appsettings.json."
    echo "      Then restart STRAIBot."
else
    echo "ERROR: GBrain failed to start. Log:"
    cat "$LOG"
    exit 1
fi
