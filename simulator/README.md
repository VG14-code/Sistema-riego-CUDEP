# Simulador virtual de dispositivos

Este proyecto emula temporalmente nodos ESP32 mientras no exista hardware físico. No contiene lógica que el backend necesite conocer: puede apagarse y sustituirse por dispositivos reales que publiquen el mismo contrato MQTT sin modificar backend ni frontend.

## Ejecución

1. Inicia Mosquitto: `docker compose up -d mosquitto`.
2. Inicia la API: `dotnet run --project backend/SistemaRiego.Api`.
3. Ejecuta: `dotnet run --project simulator/SistemaRiego.DeviceSimulator -- simulator/SistemaRiego.DeviceSimulator/simulator-settings.json`.

La configuración admite múltiples zonas y sensores. Cada sensor publica humedad o temperatura en `granja/{zona}/sensor/{sensor}/lectura` con intervalo, rango, variación y semilla aleatoria configurables.

El archivo `simulator-settings.node-a.json` asigna al nodo A la estación de bombeo `BOMBA-ABAST-01`: escucha `ENCENDER_BOMBA`/`APAGAR_BOMBA`, responde en el tópico hermano `ack` y publica nivel, presión y corriente. `simulator-settings.node-b.json` deja `pumpStation` en `null`, por lo que el nodo B no responde comandos de bomba. No hace falta levantar un simulador adicional para la bomba. Ambos nodos publican además generación/batería/consumo solar y caudal por zona. Mosquitto de `docker-compose` se reserva para staging/producción.

Los códigos de sensor y zona deben existir en los datos maestros de la API. Para una demo estable, conserva los códigos sembrados incluidos en el archivo de ejemplo.

Cuando `remoteConfigurationNodeCode` está configurado, el simulador escucha `granja/nodo/{nodo}/configuracion/comando`, aplica `CAMBIAR_FRECUENCIA`, `CAMBIAR_LIMITES` o `REINICIAR`, y publica el resultado correlacionado en `granja/nodo/{nodo}/configuracion/ack`.

## Escenarios de seguridad

Usa `SIMULATOR_TANK_LEVEL_LITERS` para iniciar con un nivel específico y `SIMULATE_PUMP_OVERCURRENT=true` para que una bomba encendida reporte 18 A, dispare la parada automática y deje una falla pendiente de reconocimiento.
