param(
  [string]$BaseUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Stop"

function Post-Chat() { Invoke-RestMethod -Method Post "$BaseUrl/chats" }
function Get-Chat([string]$id) { Invoke-RestMethod "$BaseUrl/chats/$id" }

Write-Host "`n=== RR quick test: create 5 chats (TeamA only) ===" -ForegroundColor Cyan

$ids = 1..5 | ForEach-Object { (Post-Chat).chatId }

Start-Sleep -Seconds 1

$chats = $ids | ForEach-Object { Get-Chat $_ }

# Agrupa por agente
$groups = $chats | Group-Object AssignedAgentId | Sort-Object Count -Descending

Write-Host "`nDistribution by AssignedAgentId:" -ForegroundColor Cyan
$groups | ForEach-Object {
  "{0} -> {1}" -f $_.Name, $_.Count
}

if ($groups.Count -ne 2) {
  Write-Host "`nFAIL: Esperado 2 agentes (Junior e Senior), mas vi $($groups.Count)." -ForegroundColor Red
  Write-Host "Isso significa que ainda existem outros agentes seedados/ativos." -ForegroundColor Yellow
  exit 1
}

$counts = $groups | Select-Object -ExpandProperty Count
Write-Host "`nCounts: $($counts -join ', ')" -ForegroundColor DarkGray

if ($counts[0] -eq 4 -and $counts[1] -eq 1) {
  Write-Host "PASS: Bateu o exemplo do PDF (4/1)" -ForegroundColor Green
  exit 0
}

if ($counts[0] -eq 3 -and $counts[1] -eq 2) {
  Write-Host "WARN: Deu 3/2. Seu RR parece 'alternado puro' e NÃO weighted por capacidade." -ForegroundColor Yellow
  Write-Host "Se o PDF exige 4/1, o assigner precisa usar 'peso/capacidade' (Junior=4, Senior=7) e preferência." -ForegroundColor Yellow
  exit 2
}

Write-Host "FAIL: Distribuição inesperada. Verifique se o dispatcher está atribuindo e se há só 2 agentes." -ForegroundColor Red
exit 3
