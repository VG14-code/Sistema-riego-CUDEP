# Simulador virtual de dispositivos

Este proyecto emula temporalmente nodos ESP32 mientras no exista hardware físico. No contiene lógica que el backend necesite conocer: puede apagarse y sustituirse por dispositivos reales que publiquen el mismo contrato MQTT sin modificar backend ni frontend.

## Ejecución

1. Inicia Mosquitto: `docker compose up -d mosquitto`.
2. Inicia la API: `dotnet run --project backend/SistemaRiego.Api`.
3. Ejecuta: `dotnet run --project simulator/SistemaRiego.DeviceSimulator -- simulator/SistemaRiego.DeviceSimulator/simulator-settings.json`.

La configuración admite múltiples zonas y sensores. Cada sensor publica humedad o temperatura en `granja/{zona}/sensor/{sensor}/lectura` con intervalo, rango, variación y semilla aleatoria configurables.

El simulador escucha `granja/+/valvula/+/comando` y, tras un retardo configurable, responde en el tópico hermano `ack` con “válvula abierta” o “válvula cerrada”.

Los códigos de sensor y zona deben existir en los datos maestros de la API. Para una demo estable, conserva los códigos sembrados incluidos en el archivo de ejemplo.
