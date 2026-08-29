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

## Datos de demostración

El seeder distingue dos capas y solo la segunda es sintética.

- **Estructura permanente (siempre se siembra):** catálogos maestros, permisos, roles, usuario administrador, tipos y cultivos de referencia, inventario IoT, territorio CUDEP con sus tres sectores y zonas, parámetros globales, tanque, bomba y plan de mantenimiento. Describe la instalación real y es editable desde la interfaz.
- **Muestra de demostración (condicional):** 84 días de lecturas, riegos, consumos y eventos generados por fórmula en `EmpiricalDashboardSeeder.SeedDemoHistoryAsync`, los 6 riegos históricos de `Modules7To10Seeder` y las 24 lecturas iniciales de `DbSeeder`. **No provienen de hardware.** Alimentan las tendencias y exportaciones para poder probarlas sin esperar meses de telemetría real.

La muestra se siembra únicamente cuando el entorno es `Development`. Para forzar el comportamiento en cualquier entorno, usa la clave `Seed:IncludeDemoData`:

~~~json
{
  "Seed": {
    "IncludeDemoData": false
  }
}
~~~

Con `false` en `Development` obtienes una base limpia, útil para demostrar el sistema con telemetría exclusivamente real. En `Staging` y `Production` el valor predeterminado ya es `false`, de modo que un despliegue nunca mezcla datos fabricados con lecturas de dispositivos.

Toda lectura sembrada declara su origen en `Transport` (`SIMULACIÓN` o `MUESTRA_CAMPO_PRUEBA`), y la muestra de 84 días queda marcada con el evento `DEMO_EMPIRICAL_DATA_V1`. Las pruebas `DbSeederDemoDataTests` verifican que sin la bandera no queda ningún historial fabricado y que la estructura operativa sí se crea.

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

## Recuperación de contraseña

El login expone **¿Olvidaste tu contraseña?** y siempre responde: “Si el correo está registrado, recibirás un enlace en unos minutos.” El enlace vence en 30 minutos, solo se acepta una vez y, al completarse, revoca los refresh tokens y los JWT activos mediante el sello de seguridad de Identity. Las solicitudes y los cambios se auditan sin guardar contraseñas ni tokens.

### Proveedor de correo en Development

Development queda configurado con `Email:Provider = Smtp` en `appsettings.Development.json`. La configuración versionada contiene únicamente datos no sensibles: `smtp.gmail.com:587`, STARTTLS, remitente y timeout. La contraseña de aplicación vive exclusivamente en .NET User Secrets bajo `Email:SmtpPassword`; nunca debe copiarse a `appsettings`, logs, capturas ni commits.

Para usar Gmail SMTP:

~~~powershell
dotnet user-secrets set "Email:Provider" "Smtp" --project .\backend\SistemaRiego.Api\SistemaRiego.Api.csproj
# Email:SmtpPassword debe existir previamente en User Secrets.
~~~

Para alternar al buzón de archivos sin enviar correo real:

~~~powershell
dotnet user-secrets set "Email:Provider" "File" --project .\backend\SistemaRiego.Api\SistemaRiego.Api.csproj
~~~

Reinicia la API después de cambiar el proveedor. En modo `File`, cada solicitud válida crea un HTML ignorado por Git en:

~~~text
backend/SistemaRiego.Api/dev-mailbox/password-recovery-*.html
~~~

Cuando `Smtp` está activo en Development, `ArchiveSentMessages` guarda en esa misma carpeta una copia de verificación **solo después** de que Gmail acepta el mensaje; no funciona como fallback ante un fallo SMTP.

Para diagnosticar un envío, confirma al arrancar la línea `Proveedor de correo activo: Smtp` y revisa la consola o `backend/SistemaRiego.Api/logs/sistema-riego-YYYYMMDD.log`. Busca `Servidor SMTP ... aceptó el correo` o `No fue posible entregar el correo de recuperación`. El usuario siempre recibe el mensaje neutro y nunca detalles internos. Verifica host `smtp.gmail.com`, puerto `587`, STARTTLS, usuario remitente y que `Email:SmtpPassword` exista en User Secrets. Si Google revoca o invalida la App Password, revócala/regenera una nueva desde la seguridad de la cuenta de Google y actualiza únicamente User Secrets.

En producción se usan variables de entorno o el almacén de secretos del despliegue; nunca `appsettings.json`:

~~~text
Email__Provider=Smtp
Email__SmtpHost=smtp.gmail.com
Email__SmtpPort=587
Email__SmtpUsername=vgbm123456@gmail.com
Email__SmtpPassword=<secreto administrado fuera de Git>
Email__EnableSsl=true
Email__FrontendBaseUrl=https://<dominio-del-frontend>
~~~

### Respaldo administrativo y cambio obligatorio

En **Usuarios**, cada fila ofrece **Restablecer contraseña**. La operación exige el TOTP vigente del administrador, revoca todas las sesiones del usuario y muestra una contraseña temporal una única vez. Al iniciar sesión con ella solo se permite la pantalla de cambio obligatorio; el resto de la API responde `403` hasta que el usuario defina su contraseña definitiva.

Validación visual reproducible, con backend y frontend activos:

~~~powershell
cd .\frontend
$env:E2E_PASSWORD = "<contraseña actual del administrador>"
node .\scripts\password-recovery-e2e.mjs
~~~

El escenario cubre los nueve casos solicitados, restaura la contraseña original, conserva la configuración TOTP, elimina los tokens de recuperación creados y guarda el reporte/capturas en `artifacts/password-recovery/`.

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