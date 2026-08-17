$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5080/api'
$login = Invoke-RestMethod -Method Post -Uri "$base/auth/login" -ContentType 'application/json' -Body (@{ email='admin@sistemariego.local'; password= } | ConvertTo-Json)
$headers = @{ Authorization = "Bearer $($login.accessToken)" }

$rules = Invoke-RestMethod -Headers $headers -Uri "$base/automation/rules"
$evaluation = Invoke-RestMethod -Method Post -Headers $headers -Uri "$base/automation/evaluate"
$supply = Invoke-RestMethod -Headers $headers -Uri "$base/water-supply/status"
$zones = Invoke-RestMethod -Headers $headers -Uri "$base/manual-irrigation/zones"

if (-not $zones -or -not $zones[0].id) { throw 'No hay zonas disponibles para probar el riego manual.' }
$manualBody = @{
  irrigationZoneId = $zones[0].id
  durationMinutes = 5
  flowRateLitersMinute = 8
  reason = 'Prueba funcional automatizada'
  observations = 'Este evento confirma el flujo del módulo 9.'
} | ConvertTo-Json
$started = Invoke-RestMethod -Method Post -Headers $headers -Uri "$base/manual-irrigation/start" -ContentType 'application/json' -Body $manualBody
$stopped = Invoke-RestMethod -Method Post -Headers $headers -Uri "$base/manual-irrigation/$($started.id)/stop" -ContentType 'application/json' -Body (@{ observations='Prueba funcional completada.' } | ConvertTo-Json)
$summary = Invoke-RestMethod -Headers $headers -Uri "$base/operations/summary?days=30"
$history = Invoke-RestMethod -Headers $headers -Uri "$base/operations/history?take=20"
$csv = Invoke-WebRequest -UseBasicParsing -Headers $headers -Uri "$base/operations/export.csv"

[pscustomobject]@{
  Rules = $rules.Count
  Evaluated = $evaluation.results.Count
  Tanks = $supply.Count
  Zones = $zones.Count
  ManualVolumeLiters = $stopped.volumeLiters
  ConsumptionEvents = $summary.eventCount
  OperationalEvents = $history.Count
  CsvBytes = $csv.RawContentLength
} | Format-List
