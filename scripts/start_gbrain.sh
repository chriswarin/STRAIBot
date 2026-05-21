#!/bin/bash
# scripts/start_gbrain.sh
#
# Starts the GBrain MCP HTTP server bound to all interfaces so STRAIBot
# (running on Windows) can reach it from PowerShell via the WSL IP.
#
# Requirements:
#   - Bun installed at ~/.bun/bin/bun
#   - GBrain installed:   bun install -g gbrain
#   - GBrain initialised: GBRAIN_EMBEDDING_MODEL=openai:text-embedding-3-large gbrain init --pglite
#   - Memory imported:    GBRAIN_EMBEDDING_MODEL=openai:text-embedding-3-large gbrain import /mnt/c/Users/chris/source/repos/STRAIBot/memory
#   - OPENAI_API_KEY set in environment
#
# After starting:
#   1. Copy the Admin Token printed in the banner (also in /tmp/gbrain.log).
#   2. Run scripts/register_gbrain_client.ps1 from PowerShell to register a client
#      and get an access token.
#   3. Update Memory:GBrainAccessToken in STRAIBot/appsettings.json.
#
# Access tokens expire after 1 hour — re-run register_gbrain_client.ps1
# (token step only) and update appsettings.json when expired.
#
# Run from PowerShell:
#   wsl bash -c "tr -d '\r' < /mnt/c/Users/chris/source/repos/STRAIBot/scripts/start_gbrain.sh > /tmp/sg.sh && bash /tmp/sg.sh"

set -e

BRUN="/home/chris/.bun/bin/bun"
GBRAIN_BIN="/home/chris/.bun/bin/gbrain"
LOG="/tmp/gbrain.log"
PORT=3131

if pgrep -f "gbrain serve" > /dev/null 2>&1; then
    echo "Stopping existing GBrain process..."
    pkill -f "gbrain serve" || true
    sleep 1
fi

> "$LOG"

echo "Starting GBrain MCP server on port $PORT (bind 0.0.0.0, DCR enabled)..."

nohup "$BRUN" "$GBRAIN_BIN" serve \
    --http \
    --port "$PORT" \
    --enable-dcr \
    --bind 0.0.0.0 \
    >> "$LOG" 2>&1 &

disown
sleep 4

if pgrep -f "gbrain serve" > /dev/null 2>&1; then
    echo ""
    echo "GBrain is running. Startup banner:"
    echo ""
    cat "$LOG"
    echo ""
    echo "Next steps:"
    echo "  1. Copy the Admin Token from the banner above."
    echo "  2. Run: scripts/register_gbrain_client.ps1 from PowerShell."
    echo "  3. Update Memory:GBrainAccessToken in STRAIBot/appsettings.json."
    echo "  4. dotnet run --project STRAIBot --launch-profile http"
else
    echo "GBrain failed to start. Log:"
    cat "$LOG"
    exit 1
fi
