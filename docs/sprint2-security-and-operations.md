# Sprint 2: seguridad y operación

## Secretos por entorno

El repositorio no contiene valores de credenciales. En `Development`, la API obtiene `Jwt:SigningKey`, `SeedAdmin:Password`, `Mqtt:Password` y la lista `Mqtt:AllowedClients` desde .NET User Secrets. El simulador obtiene su contraseña de `SISTEMA_RIEGO_MQTT_PASSWORD`.

En staging y producción deben suministrarse mediante el almacén de secretos del despliegue o variables de entorno:

- `Jwt__SigningKey`
- `SeedAdmin__Password` (solo cuando se requiera sembrar el administrador inicial)
- `Mqtt__Password`
- `Mqtt__AllowedClients__0__ClientId`, `Mqtt__AllowedClients__0__Username` y `Mqtt__AllowedClients__0__Password`, repitiendo el índice por cliente
- `SISTEMA_RIEGO_MQTT_PASSWORD` para cada proceso de simulación autorizado

El endpoint de alta de usuarios no es público: requiere la política `Administrator` y un código TOTP válido. Login y recuperación de contraseña tienen un límite fijo de cinco solicitudes por minuto y dirección IP.

## TOTP

Cada administrador configura su autenticador en **Administración → Seguridad 2FA**. Las operaciones de riego manual, cambios de umbrales/reglas de automatización y gestión de usuarios/roles exigen el encabezado `X-TOTP-Code`.

## ET0

El cálculo ET0 no se improvisó en este sprint. La propuesta técnica y las entradas requeridas están documentadas en [et0-reference.md](./et0-reference.md).
