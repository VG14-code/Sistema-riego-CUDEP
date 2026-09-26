# Estado de módulos — cierre funcional

Fecha de corte original: 12 de septiembre de 2026. Cierre técnico actualizado: 19 de septiembre de 2026.

## Resultado ejecutivo

La referencia funcional es la hoja «Módulos detallados» de `Planificacion final Victor Gabriel Madrid .xlsx`. Los módulos 1 al 17 están completos en software, con pantalla, API, persistencia, permisos y validación. El módulo 18, Implementación y capacitación, permanece pendiente porque comprende el despliegue, publicación, instalación física, configuración de credenciales externas y capacitación en campo.

Los módulos 8 al 17 se cerraron con una migración acumulativa, pantallas de administración por módulo, procesos automáticos protegidos y pruebas específicas. Los comandos físicos usan MQTT y quedan sujetos a las protecciones existentes y a confirmación ACK. Las integraciones externas solo operan cuando sus URL o credenciales se configuran durante el despliegue.

## Estado por módulo

| Módulo | Estado | Evidencia funcional | Pendiente |
|---|---|---|---|
| 1. Inicio | Completo | Resumen operativo, mapa, telemetría y actividad reciente. | — |
| 2. Datos maestros | Completo | Centros, fincas, zonas, catálogos, unidades, modelos, suelos y cultivos. | — |
| 3. Seguridad | Completo | Usuarios, CRUD de roles, permisos, sesiones, JWT, 2FA y renovación segura. | — |
| 4. Infraestructura IoT | Completo | Dispositivos, nodos, instalación, configuración remota, firmware e inventario. | — |
| 5. Sensores y lecturas | Completo | Sensores, frecuencia remota, calibración, lecturas, paginación y calidad. | — |
| 6. Gestión agronómica | Completo | Requerimientos, etapas, suelos, condiciones, recomendación y aprobación asistida. | — |
| 7. Planificación de cultivos | Completo | Ciclos, asignación, etapas, rotación y calendario agrícola. | — |
| 8. Sectores y zonas de riego | Completo | Configuración hidráulica por zona: fuente, tanque, bomba, tubería, caudal, presión y estado operativo. | — |
| 9. Automatización | Completo | Alta y edición de reglas con 2FA, simulación sin activar equipos, ejecución real, historial de todas las evaluaciones y versionado. | — |
| 10. Operación de riego | Completo | Riego manual, asistido, automático y programaciones futuras únicas o recurrentes con ejecución MQTT y ACK. | — |
| 11. Tanque y bombeo | Completo | CRUD de tanques y fuentes, operación manual, llenado automático por umbrales, antirrepetición, protecciones e historial. | — |
| 12. Energía solar | Completo | Monitoreo y CRUD de paneles, baterías y controladores; historial y cálculo de autonomía. | — |
| 13. Consumo y eficiencia | Completo | Consumo por zona/sector/cultivo, costo, líneas base, ahorro y detección configurable de anomalías. | — |
| 14. Mantenimiento | Completo | Planes, incidencias, preventivo/correctivo, órdenes con técnico y compromiso e historial por dispositivo. | — |
| 15. Notificaciones y alertas | Completo | Reglas por tipo/severidad, correo, Telegram, Teams y n8n, entregas auditadas, escalamiento y resúmenes diario/semanal automáticos. | — |
| 16. Reportería y analítica | Completo | PDF de consumo, riego, mantenimiento, lecturas, alertas, IoT y energía; Excel/CSV/JSON; vistas, relaciones y medidas DAX; prueba de conexión Power BI. | — |
| 17. Auditoría y configuración | Completo | Auditoría paginada, versionado de reglas, nombre del sistema, zona horaria, formato, n8n, Power BI y parámetros globales. | — |
| 18. Implementación y capacitación | Pendiente | Fuera del cierre de desarrollo. | Despliegue, instalación física, secretos/credenciales reales, carga inicial y capacitación. |

## Controles operativos incorporados

- Los riegos programados validan zona activa, riego concurrente, capacidad hidráulica y reserva segura antes de publicar la apertura MQTT.
- El llenado automático valida fuente, caudal, tanque, bloqueo/falla de bomba y evita duplicar comandos mientras existe una orden pendiente de ACK.
- Las alertas aplican reglas por tipo y severidad; cada entrega conserva canal, intento, estado, código HTTP y error.
- Los resúmenes diario y semanal respetan la zona horaria del sistema y evitan duplicados mediante el historial de integraciones.
- La simulación de reglas persiste su decisión, pero nunca crea un riego ni envía comandos.
- Las operaciones de gestión energética y eficiencia cuentan con permisos de escritura separados de los permisos de consulta.

## Validación final

- Backend: **214 de 214 pruebas .NET superadas**.
- Auditoría del módulo 1 sobre la API en marcha: los indicadores del resumen coinciden con la base de datos, el mapa entrega los cinco niveles, SignalR difunde lecturas y alertas en vivo y la actividad reciente mezcla auditoría, telemetría, riego y comandos.
- Correcciones de esa auditoría: el nivel del tanque es obligatorio (una petición sin él vaciaba el tanque) y los riegos programados respetan el máximo de válvulas simultáneas.
- Los catálogos de los módulos 8 al 15 ya no aceptan texto libre: la recurrencia de una programación, el tipo y el estado de una orden de trabajo, el canal y la severidad de una regla de notificación, los litros y el umbral de una línea base y el caudal de una fuente se validan contra los valores que ofrece la pantalla.
- Frontend: **typecheck, ESLint y build de producción superados**.
- Backend API: compilación .NET 10 sin errores ni advertencias.
- Simulador MQTT: compilación .NET 10 sin errores ni advertencias.
- Entity Framework: migración `CompleteModules8To17` creada y modelo sin cambios pendientes.
- Despliegue: no ejecutado, conforme al alcance solicitado.

La evidencia de los módulos 1 al 7 permanece en `docs/trazabilidad-modulos-1-7.md`. Las instrucciones de operación local y simulación se encuentran en `README.md`.