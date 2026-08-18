# Sistema de Riego

## MQTT por entorno

- **Development:** el backend inicia automáticamente un broker MQTT embebido con `MQTTnet.Server` en el host y puerto definidos en `Mqtt` (por defecto `localhost:1883`). El simulador virtual usa ese mismo destino, por lo que no requiere Docker ni una instalación externa.
- **Staging/Production:** el broker embebido no se registra. El backend se conecta al broker externo configurado; `docker-compose.yml` y `infrastructure/mosquitto/` conservan Mosquitto como opción de despliegue prevista para el Sprint 7.

No se debe iniciar Mosquitto localmente en el puerto `1883` al mismo tiempo que el backend en `Development`, porque ambos intentarían reservar el mismo puerto.

## Arranque local completo (PowerShell)

No se requiere Docker. Abre terminales separadas y conserva este orden.

1. Backend y broker MQTT embebido:

   ~~~powershell
   cd "C:\Users\VICTOR\Desktop\Sistema de Riego"
   $env:ASPNETCORE_ENVIRONMENT = "Development"
   dotnet run --project .\backend\SistemaRiego.Api\SistemaRiego.Api.csproj --urls http://localhost:5080
   ~~~

   La API queda en `http://localhost:5080`. Al iniciar ejecuta automáticamente las migraciones de LocalDB y los seeders.

2. Frontend:

   ~~~powershell
   cd "C:\Users\VICTOR\Desktop\Sistema de Riego\frontend"
   npm install
   npm run dev -- --host localhost --port 5173 --strictPort
   ~~~

   `npm install` solo es necesario la primera vez o cuando cambia `package-lock.json`. La aplicación queda en `http://localhost:5173`.

3. Un simulador que cubre las zonas A1 y B1:

   ~~~powershell
   cd "C:\Users\VICTOR\Desktop\Sistema de Riego\simulator\SistemaRiego.DeviceSimulator"
   $env:SISTEMA_RIEGO_MQTT_PASSWORD = "<contraseña del cliente esp32-virtual-cudep>"
   dotnet run -- .\simulator-settings.json
   ~~~

   Para simular dos nodos independientes, usa dos terminales: `simulator-settings.node-a.json` con el secreto de `esp32-virtual-cudep`, y `simulator-settings.node-b.json` con el de `esp32-virtual-norte`. No ejecutes al mismo tiempo el archivo predeterminado y node-a porque comparten ClientId.

La telemetría sigue el flujo simulador → MQTT → servicio de ingestión → base de datos → SignalR (`telemetryReadingReceived`). Los comandos se publican en `granja/{zona}/valvula/{dispositivo}/comando`; el simulador responde en el tópico `/ack` y el backend marca el comando como confirmado.

## Credenciales y 2FA de desarrollo

El administrador sembrado usa `admin@sistemariego.local`. La contraseña y los secretos MQTT no se guardan en Git; consulta los valores configurados en esta máquina con:

~~~powershell
cd "C:\Users\VICTOR\Desktop\Sistema de Riego"
dotnet user-secrets list --project .\backend\SistemaRiego.Api\SistemaRiego.Api.csproj
~~~

Una cuenta recién sembrada inicia con 2FA desactivado. Para operaciones críticas, entra en **Seguridad 2FA**, genera la clave, agrégala a una aplicación TOTP y confirma con el código de seis dígitos. No existe un secreto TOTP fijo de pruebas; generar una clave reemplaza la anterior.

## Seguridad y simulación multi-nodo

En Development, el broker exige credenciales por cliente MQTT. Las credenciales de la API se cargan desde .NET User Secrets y el simulador recibe `SISTEMA_RIEGO_MQTT_PASSWORD`; no se almacenan en `appsettings*.json`. Producción debe usar variables de entorno o su almacén de secretos.

Identity, TOTP, variables requeridas y operación segura están documentados en [docs/sprint2-security-and-operations.md](docs/sprint2-security-and-operations.md).

Los polígonos territoriales, el scheduler, el límite global de válvulas, el flujo MQTT/ACK, el watchdog, el paro total y la reconciliación están documentados en [docs/sprint3-territory-automation-operations.md](docs/sprint3-territory-automation-operations.md).

## Retención de telemetría

La estrategia, los índices y el tratamiento de mensajes atrasados están documentados en docs/telemetry-retention.md. La ventana caliente propuesta es de 180 días; el archivado automatizado se implementará junto con la infraestructura de producción.

El control hidráulico M11, energía solar M12 y consumo/eficiencia M13 están documentados en [docs/sprint4-water-energy-consumption.md](docs/sprint4-water-energy-consumption.md).

Alertas en tiempo real, webhook/n8n y mantenimiento preventivo/correctivo están documentados en [docs/sprint5-alerts-maintenance.md](docs/sprint5-alerts-maintenance.md).

Los reportes PDF/Excel, la auditoría sensible con CorrelationId/retención y la guía reproducible de Power BI están documentados en [docs/sprint6-reporting-audit-powerbi.md](docs/sprint6-reporting-audit-powerbi.md).
