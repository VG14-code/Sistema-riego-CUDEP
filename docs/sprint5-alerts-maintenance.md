# Sprint 5 · Alertas y mantenimiento

## Fuentes conectadas

El motor crea alertas idempotentes desde condiciones que ya detectaba el sistema:

- comandos MQTT y cierres sin ACK (`IrrigationWatchdogWorker`);
- trabajo en seco y sobrecorriente de bomba;
- paro global de emergencia;
- nodos, dispositivos y sensores sin heartbeat (`IoTHealthWorker`);
- batería baja o falta de generación solar diurna;
- desviación de consumo igual o superior al 25 % frente al requerimiento agronómico.

El `Fingerprint` evita duplicar una condición mientras su alerta siga activa o reconocida. Una condición resuelta puede volver a generar una alerta nueva. El campo `Origin` distingue `Condición detectada` de `Prueba manual`.

## Escalamiento e incidencias

`ALERT_ESCALATION_MINUTES` controla la re-notificación de alertas críticas no reconocidas. `MAINTENANCE_INCIDENT_MINUTES` controla cuándo se genera una incidencia automática vinculada. El procesador garantiza una sola incidencia por alerta, y el intervalo evita reintentos continuos.

## Webhook y n8n

Configura `Alerts:WebhookUrl` mediante variable de entorno o almacén de secretos; por ejemplo `Alerts__WebhookUrl=https://n8n.example/webhook/sistema-riego`. El backend enviará un `POST` JSON con ID, tipo, severidad, estado, origen, descripción, equipo, fecha y nivel de escalamiento. Cada intento queda en `NotificationDeliveries` con estado y código HTTP.

En n8n basta crear un nodo **Webhook** `POST`, copiar su URL a la configuración anterior y conectar después los nodos deseados (correo, Telegram, Teams o registro). El contrato es genérico, por lo que no requiere cambios posteriores en el backend.

## API

- `GET /api/alerts` admite filtros por severidad, tipo, estado y entidad.
- `POST /api/alerts/{id}/acknowledge` registra usuario y fecha.
- `POST /api/alerts/{id}/resolve` cierra la condición.
- `/api/maintenance/plans`, `/activities` e `/incidents` ofrecen creación, consulta, actualización y eliminación.
- `GET /api/maintenance/equipment/{tipo}/{id}/history` consolida el historial del equipo.
