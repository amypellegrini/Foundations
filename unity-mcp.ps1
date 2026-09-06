<# Local MCP client for the official MCP for Unity server. #>
param([string]$Method = 'tools/list', [string]$Params = '{}')
$ErrorActionPreference = 'Stop'
$endpoint = 'http://127.0.0.1:8080/mcp'
$headers = @{Accept='application/json, text/event-stream'}
$init = @{jsonrpc='2.0';id=1;method='initialize';params=@{protocolVersion='2024-11-05';capabilities=@{};clientInfo=@{name='Foundations';version='1.0'}}} | ConvertTo-Json -Depth 10
$response = Invoke-WebRequest -Uri $endpoint -Method Post -Headers $headers -ContentType 'application/json' -Body $init
$headers['Mcp-Session-Id'] = [string]($response.Headers['Mcp-Session-Id'] | Select-Object -First 1)
Invoke-WebRequest -Uri $endpoint -Method Post -Headers $headers -ContentType 'application/json' -Body '{"jsonrpc":"2.0","method":"notifications/initialized"}' | Out-Null
$body = @{jsonrpc='2.0';id=2;method=$Method;params=($Params | ConvertFrom-Json)} | ConvertTo-Json -Depth 30
$reply = Invoke-WebRequest -Uri $endpoint -Method Post -Headers $headers -ContentType 'application/json' -Body $body -TimeoutSec 90
if ($reply.Content -match '(?m)^data: (.+)$') { $Matches[1] } else { $reply.Content }
