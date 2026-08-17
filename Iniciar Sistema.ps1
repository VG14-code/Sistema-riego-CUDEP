$ErrorActionPreference = 'Stop'

$projectRoot = $PSScriptRoot
$apiProject = Join-Path $projectRoot 'backend\SistemaRiego.Api\SistemaRiego.Api.csproj'
$frontendRoot = Join-Path $projectRoot 'frontend'
$viteCli = Join-Path $frontendRoot 'node_modules\vite\bin\vite.js'

function Test-LocalPort([int]$Port) {
    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $connection = $client.ConnectAsync('127.0.0.1', $Port)
        return $connection.Wait(500) -and $client.Connected
    }
    catch { return $false }
    finally { $client.Dispose() }
}

if (-not (Test-Path -LiteralPath $apiProject)) { throw "No se encontró el proyecto de la API: $apiProject" }
if (-not (Test-Path -LiteralPath $viteCli)) { throw "No se encontró Vite. Ejecuta npm install dentro de la carpeta frontend." }

if (-not (Test-LocalPort 5080)) {
    $apiArguments = "run --project `"$apiProject`" --no-build --launch-profile http"
    Start-Process 'dotnet.exe' -ArgumentList $apiArguments -WorkingDirectory $projectRoot -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $projectRoot 'backend\api-running.log') `
        -RedirectStandardError (Join-Path $projectRoot 'backend\api-running-error.log')
    Write-Host 'Iniciando API...' -ForegroundColor Cyan
}
else { Write-Host 'La API ya está activa.' -ForegroundColor Green }

if (-not (Test-LocalPort 5173)) {
    $viteArguments = "`"$viteCli`" --host 127.0.0.1 --port 5173"
    Start-Process 'node.exe' -ArgumentList $viteArguments -WorkingDirectory $frontendRoot -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $frontendRoot 'vite-running.log') `
        -RedirectStandardError (Join-Path $frontendRoot 'vite-running-error.log')
    Write-Host 'Iniciando interfaz web...' -ForegroundColor Cyan
}
else { Write-Host 'La interfaz web ya está activa.' -ForegroundColor Green }

for ($attempt = 1; $attempt -le 20; $attempt++) {
    if ((Test-LocalPort 5080) -and (Test-LocalPort 5173)) { break }
    Start-Sleep -Milliseconds 500
}

if (-not (Test-LocalPort 5080)) { throw 'La API no pudo iniciar. Revisa backend\api-running-error.log.' }
if (-not (Test-LocalPort 5173)) { throw 'La interfaz no pudo iniciar. Revisa frontend\vite-running-error.log.' }

Write-Host ''
Write-Host 'Sistema de Riego listo' -ForegroundColor Green
Write-Host 'Web: http://127.0.0.1:5173/'
Write-Host 'API: http://127.0.0.1:5080/'
