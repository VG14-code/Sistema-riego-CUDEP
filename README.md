# Sistema de Riego

## MQTT por entorno

- **Development:** el backend inicia automáticamente un broker MQTT embebido con `MQTTnet.Server` en el host y puerto definidos en `Mqtt` (por defecto `localhost:1883`). El simulador virtual usa ese mismo destino, por lo que no requiere Docker ni una instalación externa.
- **Staging/Production:** el broker embebido no se registra. El backend se conecta al broker externo configurado; `docker-compose.yml` y `infrastructure/mosquitto/` conservan Mosquitto como opción de despliegue prevista para el Sprint 7.

No se debe iniciar Mosquitto localmente en el puerto `1883` al mismo tiempo que el backend en `Development`, porque ambos intentarían reservar el mismo puerto.

## Arranque local

1. Ejecutar el backend con `ASPNETCORE_ENVIRONMENT=Development`.
2. Ejecutar `simulator/SistemaRiego.DeviceSimulator`; su configuración predeterminada apunta a `localhost:1883`.
3. Ejecutar el frontend con `npm run dev` desde `frontend/`.

La telemetría sigue el flujo simulador → MQTT → servicio de ingestión → base de datos → SignalR (`telemetryReadingReceived`). Los comandos se publican en `granja/{zona}/valvula/{dispositivo}/comando`; el simulador responde en el tópico `/ack` y el backend marca el comando como confirmado.


## Seguridad y simulación multi-nodo

En Development, el broker exige credenciales por cliente MQTT. Las credenciales de la API se cargan desde .NET User Secrets y el simulador recibe `SISTEMA_RIEGO_MQTT_PASSWORD`; no se almacenan en `appsettings*.json`. Producción debe usar variables de entorno o su almacén de secretos. Para simular dos nodos en paralelo, inicia dos procesos pasando `simulator-settings.node-a.json` y `simulator-settings.node-b.json`.

Identity, TOTP, variables requeridas y operación segura están documentados en [docs/sprint2-security-and-operations.md](docs/sprint2-security-and-operations.md).

Los polígonos territoriales, el scheduler, el límite global de válvulas, el flujo MQTT/ACK, el watchdog, el paro total y la reconciliación están documentados en [docs/sprint3-territory-automation-operations.md](docs/sprint3-territory-automation-operations.md).

## Retención de telemetría

La estrategia, los índices y el tratamiento de mensajes atrasados están documentados en docs/telemetry-retention.md. La ventana caliente propuesta es de 180 días; el archivado automatizado se implementará junto con la infraestructura de producción.

El control hidráulico M11, energía solar M12 y consumo/eficiencia M13 están documentados en [docs/sprint4-water-energy-consumption.md](docs/sprint4-water-energy-consumption.md).

Alertas en tiempo real, webhook/n8n y mantenimiento preventivo/correctivo están documentados en [docs/sprint5-alerts-maintenance.md](docs/sprint5-alerts-maintenance.md).

Los reportes PDF/Excel, la auditoría sensible con CorrelationId/retención y la guía reproducible de Power BI están documentados en [docs/sprint6-reporting-audit-powerbi.md](docs/sprint6-reporting-audit-powerbi.md).
