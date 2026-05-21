# scripts/register_gbrain_client.ps1
#
# Registers STRAIBot as an OAuth 2.1 client with the local GBrain MCP server
# and exchanges credentials for a bearer access token.
#
# Run AFTER start_gbrain.sh has started GBrain successfully.
#
# Usage:
#   .\scripts\register_gbrain_client.ps1
#
# The script prints the access token and the appsettings.json snippet to paste.
# Tokens expire after 1 hour. Re-run the TOKEN EXCHANGE section only to refresh.

# ── CONFIG ────────────────────────────────────────────────────────────────────
# Replace with your WSL IP: run `wsl hostname -I` to find it
$WSL_IP   = "172.23.150.171"
$PORT     = 3131
$GBRAIN   = "http://${WSL_IP}:${PORT}"

# ── HEALTH CHECK ─────────────────────────────────────────────────────────────
Write-Host "`nChecking GBrain health..." -ForegroundColor Cyan
try {
    $health = Invoke-RestMethod "$GBRAIN/health"
    Write-Host "GBrain is up: $($health | ConvertTo-Json -Compress)" -ForegroundColor Green
} catch {
    Write-Error "GBrain is not reachable at $GBRAIN. Run start_gbrain.sh first."
    exit 1
}

# ── CLIENT REGISTRATION ───────────────────────────────────────────────────────
# Only needed once — the client persists in GBrain's PGLite database.
# Skip this section and go straight to TOKEN EXCHANGE if you already have
# a client_id and client_secret.
Write-Host "`nRegistering STRAIBot OAuth client..." -ForegroundColor Cyan

$regBody = @{
    client_name                  = "straibotlocal"
    grant_types                  = @("client_credentials")
    token_endpoint_auth_method   = "client_secret_post"
    scope                        = "read"
    redirect_uris                = @("http://localhost/callback")
} | ConvertTo-Json

try {
    $reg = Invoke-RestMethod "$GBRAIN/register" `
        -Method POST `
        -ContentType "application/json" `
        -Body $regBody

    $CLIENT_ID     = $reg.client_id
    $CLIENT_SECRET = $reg.client_secret

    Write-Host "Client registered:" -ForegroundColor Green
    Write-Host "  client_id:     $CLIENT_ID"
    Write-Host "  client_secret: $CLIENT_SECRET"
    Write-Host ""
    Write-Host "Save these — you can reuse them without re-registering." -ForegroundColor Yellow
} catch {
    Write-Host "Registration failed (client may already exist). Provide credentials manually:" -ForegroundColor Yellow
    $CLIENT_ID     = Read-Host "  client_id"
    $CLIENT_SECRET = Read-Host "  client_secret"
}

# ── TOKEN EXCHANGE ────────────────────────────────────────────────────────────
Write-Host "`nExchanging credentials for access token..." -ForegroundColor Cyan

$tokenBody = "grant_type=client_credentials" +
             "&client_id=$CLIENT_ID" +
             "&client_secret=$CLIENT_SECRET" +
             "&scope=read"

$tok = Invoke-RestMethod "$GBRAIN/token" `
    -Method POST `
    -ContentType "application/x-www-form-urlencoded" `
    -Body $tokenBody

$ACCESS_TOKEN = $tok.access_token
$EXPIRES_IN   = $tok.expires_in

Write-Host "Token obtained (expires in $EXPIRES_IN seconds):" -ForegroundColor Green
Write-Host "  $ACCESS_TOKEN"

# ── APPSETTINGS SNIPPET ───────────────────────────────────────────────────────
Write-Host ""
Write-Host "Paste this into STRAIBot/appsettings.json:" -ForegroundColor Cyan
Write-Host ""
Write-Host @"
  "Memory": {
    "Provider": "GBrain",
    "FallbackToMarkdown": true,
    "GBrainBaseUrl": "$GBRAIN",
    "GBrainAccessToken": "$ACCESS_TOKEN"
  }
"@

# ── QUICK VERIFY ──────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "Verify token works against /mcp:" -ForegroundColor Cyan
$testBody = '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"query","arguments":{"query":"BlueHorizon late checkout","limit":1}}}'
try {
    $mcpResult = Invoke-RestMethod "$GBRAIN/mcp" `
        -Method POST `
        -ContentType "application/json" `
        -Headers @{ Authorization = "Bearer $ACCESS_TOKEN"; Accept = "application/json, text/event-stream" } `
        -Body $testBody
    Write-Host "MCP /query returned data. Token is valid." -ForegroundColor Green
} catch {
    Write-Warning "MCP test failed: $_"
}
