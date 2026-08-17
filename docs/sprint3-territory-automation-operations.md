# Sprint 3: territorio, automatización y operación MQTT

## Módulo 8: estructura territorial

Los sectores y zonas almacenan su límite como GeoJSON `Polygon`. La API rechaza polígonos inválidos o autocruzados, zonas fuera del sector y superposiciones entre zonas activas del mismo sector. Una zona admite múltiples sensores y válvulas mediante `IrrigationZoneSensors` e `IrrigationZoneValves`; los campos históricos de sensor principal y válvula se conservan como compatibilidad.

El dashboard Leaflet dibuja polígonos de sector y zona. Cuando un registro histórico aún no tiene geometría se conserva el marcador puntual como fallback.

## Módulo 9: automatización real

`AutomationSchedulerWorker` ejecuta periódicamente `AutomationEngine`. El intervalo se controla con `AUTOMATION_INTERVAL_SECONDS`. Para cada zona gana la regla habilitada con el menor número de prioridad; las demás quedan auditables con la decisión `Omitida por prioridad`.

El motor respeta `MAX_SIMULTANEOUS_VALVES`, ventanas horarias, días permitidos, pausas manuales, lecturas válidas y riegos ya activos. Una decisión de riego crea una ejecución en `Esperando ACK`, publica `ABRIR_VALVULA` y solo pasa a `En curso` cuando llega el ACK MQTT correlacionado.

## Módulo 10: operación segura

Cada comando incluye `commandId`, zona, ejecución y vencimiento. `IrrigationWatchdogWorker` marca como fallidos los comandos que superan `MQTT_COMMAND_TIMEOUT_SECONDS`, registra el evento y cierra automáticamente los riegos que alcanzan su duración máxima.

Las paradas publican `CERRAR_VALVULA`; el consumo se consolida al recibir el ACK de cierre. El botón **PARO TOTAL** solicita el cierre de todos los riegos activos y suspende las reglas automáticas durante 24 horas. Tras cada reconexión del backend al broker se publica `CONSULTAR_ESTADO`, y la respuesta del simulador corrige el estado local o genera un evento crítico si hay una válvula abierta sin ejecución activa.

## Verificación reproducible

- `dotnet test SistemaRiego.sln --no-restore`
- `npm run typecheck`, `npm run lint` y `npm run build` desde `frontend/`
- `node scripts/smoke/visual-sprint3.mjs` con `SMOKE_EMAIL`, `SMOKE_PASSWORD` y frontend/backend activos
- `node scripts/smoke/manual-mqtt-sprint3.mjs` con `SMOKE_EMAIL`, `SMOKE_PASSWORD`, backend y simulador activos

Las claves JWT, credenciales MQTT y contraseña del usuario semilla deben suministrarse mediante User Secrets o variables de entorno; no se versionan.
