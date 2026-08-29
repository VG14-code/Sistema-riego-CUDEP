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

### Segundo factor en el inicio de sesión

Cuando la cuenta tiene 2FA activo, `POST /api/auth/login` no devuelve token: responde `{ requiresTwoFactor, challengeToken, expiresAtUtc }`. El desafío se guarda con su hash en `TwoFactorLoginChallenges`, vive cinco minutos (`Jwt:TwoFactorChallengeMinutes`), es de un solo uso y admite cinco intentos; pedir un desafío nuevo invalida los anteriores de esa cuenta.

`POST /api/auth/login/2fa` recibe `{ email, challengeToken, code }` y solo entonces emite la sesión. Acepta el código de seis dígitos del autenticador y también los códigos de recuperación entregados al activar el 2FA, que se consumen al usarse. El desafío está ligado a la cuenta que lo pidió: presentarlo con otro correo lo invalida.

Cada paso queda en la bitácora de accesos: `LOGIN_2FA_REQUIRED` al emitir el desafío, `LOGIN_2FA_FAILED` por cada código incorrecto con el número de intento, `LOGIN_2FA_RECOVERY_CODE` cuando se entra con un código de recuperación y `LOGIN_SUCCESS` al completarse.

## ET0

El cálculo ET0 no se improvisó en este sprint. La propuesta técnica y las entradas requeridas están documentadas en [et0-reference.md](./et0-reference.md).
