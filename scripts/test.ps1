param(
  [string]$BaseUrl = "http://localhost:5000",
  [int]$InactiveAfterSeconds = 3,
  [switch]$RunRRExamples
)

$ErrorActionPreference = "Stop"

function Write-Header($text) { Write-Host "`n=== $text ===" -ForegroundColor Cyan }
function Pass($text) { Write-Host "PASS: $text" -ForegroundColor Green }
function Fail($text) { Write-Host "FAIL: $text" -ForegroundColor Red }
function Info($text) { Write-Host "INFO: $text" -ForegroundColor DarkGray }

function Try-GetJson($url) {
  try {
    return Invoke-RestMethod -Method Get $url
  } catch {
    return $null
  }
}

function Post-Chat() {
  return Invoke-RestMethod -Method Post "$BaseUrl/chats"
}

function Get-Chat([string]$id) {
  return Invoke-RestMethod -Method Get "$BaseUrl/chats/$id"
}

function Poll-Chat([string]$id) {
  return Invoke-RestMethod -Method Get "$BaseUrl/chats/$id/poll"
}

function Assert([bool]$condition, [string]$passMsg, [string]$failMsg) {
  if ($condition) { Pass $passMsg }
  else { Fail $failMsg; throw $failMsg }
}

function Wait-Seconds([int]$sec) {
  Start-Sleep -Seconds $sec
}

function Normalize-Team($teamValue) {
  # AssignedTeam pode vir como string ("TeamA") ou número (enum raw). Aceita ambos.
  if ($null -eq $teamValue) { return "" }
  $s = "$teamValue"
  return $s
}

Write-Header "0) Smoke check: OpenAPI"
$openapi = Try-GetJson "$BaseUrl/openapi/v1.json"
Assert ($null -ne $openapi) "OpenAPI disponível em /openapi/v1.json" "OpenAPI não acessível em /openapi/v1.json (API não subiu? porta errada?)"

Write-Header "1) POST /chats -> OK + chatId"
$r = Post-Chat
Assert ($null -ne $r.chatId) "POST /chats retornou chatId" "POST /chats não retornou chatId"
Assert ($r.status -eq "OK") "POST /chats retornou status OK" "POST /chats não retornou status OK"
Info ("chatId = " + $r.chatId)

Write-Header "2) Dispatcher atribui (AssignedAgentId deve existir em até 2s)"
$id = $r.chatId
$assigned = $false
for ($i=0; $i -lt 8; $i++) {
  $c = Get-Chat $id
  if ($null -ne $c.AssignedAgentId -and "$($c.AssignedAgentId)" -ne "") { $assigned = $true; break }
  Start-Sleep -Milliseconds 250
}
Assert $assigned "Chat foi atribuído a um agente (AssignedAgentId preenchido)" "Chat não foi atribuído (AssignedAgentId vazio). Seed de agentes/dispatcher/shift?"

Write-Header "3) Poll mantém vivo (não vira Inactive)"
$id2 = (Post-Chat).chatId
# Poll por ~4s (1 por segundo) - deve não ficar inactive
1..4 | ForEach-Object {
  Poll-Chat $id2 | Out-Null
  Start-Sleep -Seconds 1
}
$c2 = Get-Chat $id2
Assert ($c2.Status -ne "Inactive") "Após polling contínuo, chat NÃO virou Inactive" "Mesmo com polling, chat virou Inactive (ver InactivityAfterSeconds / clock / worker)"

Write-Header "4) Sem poll por > InactiveAfterSeconds -> Inactive"
$id3 = (Post-Chat).chatId
Wait-Seconds ($InactiveAfterSeconds + 1)
$c3 = Get-Chat $id3
Assert ($c3.Status -eq "Inactive") "Sem poll por $($InactiveAfterSeconds+1)s, chat virou Inactive" "Chat não virou Inactive (worker de inatividade não rodou? config diferente?)"

Write-Header "5) Overflow simulation (requer Testing: ForceOfficeHours=true, MainMaxQueueOverride=2, UsePressureForOverflowTrigger=true)"
Info "Este teste assume que você habilitou o modo simulação no appsettings e reiniciou a API."
$r1 = Post-Chat
$r2 = Post-Chat
Start-Sleep -Seconds 2 # dá tempo do OverflowActivationWorker ligar overflow
$r3 = Post-Chat

$team1 = Normalize-Team $r1.team
$team2 = Normalize-Team $r2.team
$team3 = Normalize-Team $r3.team

Info "Teams (POST): 1=$team1 2=$team2 3=$team3"

# Validação flexível: o 3º deve ser Overflow. Os 2 primeiros devem ser main (TeamA ou TeamC, dependendo do routing)
$thirdIsOverflow = ($team3 -match "Overflow")
Assert $thirdIsOverflow "3º chat entrou em Overflow (modo simulação)" "3º chat NÃO entrou em Overflow. Verifique appsettings Testing + OverflowActivationWorker + reinício."

# Confirma no GET também (AssignedTeam pode vir numérico se você não ajustou o GET)
$cR3 = Get-Chat $r3.chatId
$assignedTeamR3 = Normalize-Team $cR3.AssignedTeam
Info "AssignedTeam no GET do 3º = $assignedTeamR3"
if ($assignedTeamR3 -ne "" -and $assignedTeamR3 -notmatch "Overflow") {
  Info "Obs: se AssignedTeam está numérico (ex.: 0), ajuste o GET para retornar AssignedTeam.ToString()."
}

Pass "Testes automáticos principais concluídos."

if ($RunRRExamples) {
  Write-Header "6) Round Robin examples (OPCIONAL) — requer seed específico"
  Info "Você precisa ajustar o seed (Program.cs) antes de rodar estes exemplos."
  Info "Exemplo A: 1 Junior + 1 Senior no MESMO time. Exemplo B: 2 Juniors + 1 Mid no MESMO time."
  Info "Este script não consegue alterar seed remotamente; ele só valida se você já ajustou e reiniciou a API."

  # Exemplo A: 5 chats -> 4/1
  Write-Header "6A) RR Example A (1 Junior + 1 Senior): 5 chats -> 4/1"
  $idsA = @()
  1..5 | ForEach-Object { $idsA += (Post-Chat).chatId }
  Start-Sleep -Seconds 1
  $chatsA = $idsA | ForEach-Object { Get-Chat $_ }
  $groupA = $chatsA | Group-Object AssignedAgentId | Sort-Object Count -Descending
  $countsA = $groupA | Select-Object -ExpandProperty Count
  Info ("Distribuição counts: " + ($countsA -join ", "))

  $okA = ($countsA.Count -eq 2 -and $countsA[0] -eq 4 -and $countsA[1] -eq 1)
  if ($okA) { Pass "RR Example A OK (4/1)" } else { Fail "RR Example A não bateu (esperado 4/1). Confira seed e preferência." }

  # Exemplo B: 6 chats -> 3/3/0 (mid 0)
  Write-Header "6B) RR Example B (2 Juniors + 1 Mid): 6 chats -> 3/3 (mid 0)"
  $idsB = @()
  1..6 | ForEach-Object { $idsB += (Post-Chat).chatId }
  Start-Sleep -Seconds 1
  $chatsB = $idsB | ForEach-Object { Get-Chat $_ }
  $groupB = $chatsB | Group-Object AssignedAgentId | Sort-Object Count -Descending
  $countsB = $groupB | Select-Object -ExpandProperty Count
  Info ("Distribuição counts: " + ($countsB -join ", "))

  $okB = ($countsB.Count -eq 2 -and $countsB[0] -eq 3 -and $countsB[1] -eq 3)
  if ($okB) { Pass "RR Example B OK (3/3, mid 0)" } else { Fail "RR Example B não bateu (esperado 3/3 com 2 agentes). Confira seed e preferência." }
}

Write-Host "`nDONE" -ForegroundColor Cyan
