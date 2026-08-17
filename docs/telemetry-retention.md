# Retención de telemetría

La tabla `SensorReadings` conserva las lecturas recientes como almacenamiento caliente. La política operativa propuesta es mantener 180 días en la base principal y archivar por lotes las lecturas más antiguas antes de purgarlas. El trabajo automático de archivado queda deliberadamente fuera del Sprint 1 y se implementará con la infraestructura de producción.

Los índices que sostienen esta estrategia son:

- `MessageId` único, para idempotencia y rechazo de duplicados.
- `(SensorId, CapturedAtUtc)`, para series históricas por sensor y orden cronológico del dispositivo.
- `ReceivedAtUtc`, para seleccionar lotes de archivo/purga según su llegada al sistema.

Una lectura atrasada se guarda con su hora original y estado `Atrasada`, pero no reemplaza la última lectura conocida ni hace retroceder el heartbeat. La actividad se considera reciente según `ReceivedAtUtc`.
