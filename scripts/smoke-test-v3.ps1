$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'backend\SistemaRiego.Api\SistemaRiego.Api.csproj'
$stdout = Join-Path $root 'backend\api-smoke-v3.log'
$stderr = Join-Path $root 'backend\api-smoke-v3-error.log'
$arguments = "run --project `"$project`" --no-build --launch-profile http"
$process = Start-Process dotnet -ArgumentList $arguments -WorkingDirectory $root -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
try {
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        Start-Sleep -Milliseconds 500
        try { Invoke-WebRequest -Uri 'http://localhost:5080/openapi/v1.json' -UseBasicParsing | Out-Null; $ready = $true; break } catch { }
    }
    if (-not $ready) { throw "La API no inició. Revise $stdout y $stderr" }
    $body = @{ email = 'admin@sistemariego.local'; password =  } | ConvertTo-Json
    $response = Invoke-RestMethod -Method Post -Uri 'http://localhost:5080/api/auth/login' -ContentType 'application/json' -Body $body
    if ([string]::IsNullOrWhiteSpace($response.accessToken)) { throw 'La respuesta no incluyó el token JWT.' }
    [pscustomobject]@{ status = 'ok'; user = $response.user.email; roles = ($response.user.roles -join ', '); accessTokenExpiresAtUtc = $response.accessTokenExpiresAtUtc } | ConvertTo-Json
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
}
