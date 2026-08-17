$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'backend\SistemaRiego.Api\SistemaRiego.Api.csproj'
$log = Join-Path $root 'backend\api-smoke.log'
$process = Start-Process dotnet -ArgumentList @('run','--project',$project,'--no-build','--launch-profile','http') -WorkingDirectory $root -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError $log -PassThru
try {
    $ready = $false
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        Start-Sleep -Milliseconds 500
        try { Invoke-WebRequest -Uri 'http://localhost:5080/openapi/v1.json' -UseBasicParsing | Out-Null; $ready = $true; break } catch { }
    }
    if (-not $ready) { throw "La API no inició. Revise $log" }
    $body = @{ email = 'admin@sistemariego.local'; password =  } | ConvertTo-Json
    $response = Invoke-RestMethod -Method Post -Uri 'http://localhost:5080/api/auth/login' -ContentType 'application/json' -Body $body
    if ([string]::IsNullOrWhiteSpace($response.accessToken)) { throw 'La respuesta no incluyó el token JWT.' }
    [pscustomobject]@{ status = 'ok'; user = $response.user.email; roles = ($response.user.roles -join ', '); accessTokenExpiresAtUtc = $response.accessTokenExpiresAtUtc } | ConvertTo-Json
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
}
