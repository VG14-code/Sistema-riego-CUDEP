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

   El **nodo A es también el simulador oficial de la estación de bombeo** `BOMBA-ABAST-01`; no hace falta un tercer proceso. Atiende `ENCENDER_BOMBA`/`APAGAR_BOMBA`, publica sus ACK y reporta nivel, presión y corriente en `granja/estacion/telemetria`. El nodo B simula únicamente su nodo, sensores y válvulas; no se suscribe a comandos de bomba. Esta asignación evita ACK duplicados y telemetría hidráulica contradictoria cuando ambos nodos están activos.

La telemetría sigue el flujo simulador → MQTT → servicio de ingestión → base de datos → SignalR (`telemetryReadingReceived`). Los comandos se publican en `granja/{zona}/valvula/{dispositivo}/comando`; el simulador responde en el tópico `/ack` y el backend marca el comando como confirmado.

## Validación visual oficial con Playwright

El método oficial evita el controlador integrado afectado por ACL de Windows. Usa el Chromium administrado por Playwright y un perfil temporal aislado con `--no-sandbox` y `--disable-gpu`. Mantén backend, frontend y simulador ejecutándose antes de iniciar la captura.

Instalación inicial:

~~~powershell
cd "C:\Users\VICTOR\Desktop\Sistema de Riego\frontend"
npm install
npx playwright install chromium
~~~

Captura reproducible:

~~~powershell
cd "C:\Users\VICTOR\Desktop\Sistema de Riego\frontend"
$env:VISUAL_EMAIL = "admin@sistemariego.local"
$env:VISUAL_PASSWORD = Read-Host "Contraseña administrativa de Development"
npm run visual:capture
~~~

El comando espera el dashboard y una lectura real de telemetría, abre el centro de notificaciones y genera:

- `artifacts/visual-validation/dashboard-module-1.png`
- `artifacts/visual-validation/notifications-panel.png`

La reproducción literal del error del controlador integrado se conserva en `artifacts/visual-validation/acl-diagnostic.txt` y las comprobaciones CSS en `artifacts/visual-validation/capture-report.json`.

Los escenarios E2E que creen datos de catálogo deben eliminarlos en un bloque `finally` y restaurar cualquier registro permanente que modifiquen. `tank-crud-ui-e2e.mjs` elimina su tanque temporal mediante el endpoint protegido para tanques inactivos sin bombas; `crop-types-crud.mjs` elimina su tipo de cultivo temporal. Esta regla también aplica a futuros cultivos, sectores, usuarios y demás entidades de prueba.

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

### Datos agronómicos referenciales

Los cultivos sembrados de demostración (Maíz, Frijol y Arroz) usan cuatro etapas operativas para planificación hídrica. Sus duraciones son valores iniciales conservadores, no recomendaciones universales: deben ajustarse en Datos Maestros según variedad, fecha de siembra, clima y observaciones locales de la Granja Experimental CUDEP.

Criterio institucional consultado:
- FAO, duraciones aproximadas de las cuatro fases y necesidad de estimarlas localmente: https://www.fao.org/4/s2022e/s2022e07.htm
- FAO, ciclos de maíz de tierras bajas (70–130 días según material) y arroz tropical (80–140 días): https://www.fao.org/4/t0742e/T0742E11.htm
- FAO, frijol común: ciclo total usual de 90–120 días para grano seco: https://www.fao.org/land-water/databases-and-software/crop-information/bean/en/
- IRRI, arroz tropical: fase reproductiva cercana a 35 días y maduración cercana a 30 días; la fase vegetativa varía por cultivar: https://www.knowledgebank.irri.org/ericeproduction/0.2._Growth_stages_of_the_rice_plant.htm