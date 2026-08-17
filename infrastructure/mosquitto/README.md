# Mosquitto local

Ejecuta `docker compose up -d mosquitto` desde la raíz. MQTT TCP queda en `localhost:1883` y WebSockets en `localhost:9001`.

`allow_anonymous true` es exclusivamente para desarrollo local. Antes de cualquier despliegue se deben habilitar credenciales, ACL y TLS.
