# Estado de módulos — cierre funcional

Fecha de corte original: 12 de septiembre de 2026. Estado por módulo revisado el 19 de septiembre de 2026.

## Resultado ejecutivo

La referencia funcional es la hoja «Módulos detallados» de `Planificacion final Victor Gabriel Madrid .xlsx`, con 18 módulos. Los módulos 1 a 7 están completos y validados; su evidencia submódulo por submódulo está en `docs/trazabilidad-modulos-1-7.md`. Los módulos 8 a 17 tienen pantalla, API y lógica, pero todos conservan submódulos sin implementar; la tabla de abajo indica dónde está cada uno y qué falta. El módulo 18, Implementación y capacitación, queda pendiente porque reúne la publicación en producción, la instalación física en campo y la capacitación presencial.

El cronograma (`Planificacion Victor Madrid Final.xlsx`) agrupa el trabajo en 13 módulos y registra 111 actividades al 100 %, 6 al 50 % y 13 al 0 %, con un avance ponderado por horas de 83.9 %. Ese porcentaje mide las tareas del cronograma: varios submódulos de la hoja detallada (por ejemplo n8n, simulación de reglas o ahorro de agua) no tienen una tarea propia en él, así que un 100 % en el cronograma no implica que el módulo de la hoja esté completo.

- **Al 0 %:** seis actividades de despliegue y tres de capacitación/cierre, que dependen del despliegue; dos de montaje físico en campo, la fila 28 (protecciones, cajas y alimentación) y la fila 81 (sensores de nivel, relés y conexiones de la bomba), cuyo software está validado con el simulador MQTT; y las filas 113 y 114, conexión y modelo de Power BI, que no se han construido.
- **Al 50 %:** las filas 86 y 88, cuyas protecciones por software están probadas pero las eléctricas son físicas; la 115, que tiene las vistas probadas pero no el modelo de Power BI; y las filas 116, 117 y 119, a cuyo dashboard le faltan humedad, temperatura, tendencia semanal en pantalla y comparación entre periodos.
- **Adelantadas:** 34 actividades de los módulos 8 a 11 tienen fecha de inicio posterior al corte y siguen al 100 %, porque su código está en el repositorio desde el 17/08/2026. Conservan las fechas planificadas como línea base y lo indican en Observaciones.

## Estado por módulo

Numeración de la hoja «Módulos detallados». El menú lateral de la aplicación usa estos mismos números y nombres de grupo.

| Módulo | Estado | Dónde está en el sistema | Pendiente |
|---|---|---|---|
| 1. Inicio | Completo | Inicio: resumen operativo, mapa de la granja, telemetría en vivo y actividad reciente. | — |
| 2. Datos maestros | Completo | Centros, fincas y zonas; catálogos; unidades con factor y conversión; modelos IoT con precisión, voltaje y protocolo; tipos de suelo y cultivos. | — |
| 3. Seguridad | Completo | Usuarios con código de personal, centro y finca; CRUD de roles, matriz de permisos y sesiones; JWT, 2FA y renovación por cookie HttpOnly. | — |
| 4. Infraestructura IoT | Completo | Red IoT: dispositivos, nodos y trazabilidad de instalación, configuración remota, firmware, inventario y movimientos auditables desde el alta del equipo. | — |
| 5. Sensores y lecturas | Completo | Sensores con frecuencia y zona configurables (la frecuencia, en segundos, se envía al nodo con CAMBIAR_FRECUENCIA; las lecturas sin zona se asocian a la del sensor), calibración y lecturas en tiempo real, historial paginado, validación y calidad. | — |
| 6. Gestión agronómica | Completo | Requerimientos por cultivo, etapa y suelo; factor corrector aplicado; condiciones ambientales; recomendación explicable y aprobación como riego asistido. | — |
| 7. Planificación de cultivos | Completo | Ciclos y calendario: ciclos, asignación, etapa actual, rotación y calendario agrícola. | — |
| 8. Sectores y zonas de riego | Parcial | Zonas de riego (jerarquía y dispositivos por zona); capacidad simultánea con `MAX_SIMULTANEOUS_VALVES` y cálculo hidráulico en el API. | Pantalla de configuración hidráulica (fuente, tanque, bomba, tubería, caudal, presión) y gestión del estado de zona. |
| 9. Automatización | Parcial | Reglas automáticas: prioridad, ventana horaria, histéresis, duración máxima, activación y última decisión con motivo. | Alta de reglas desde la interfaz, simulación sin activar equipos («Evaluar ahora» ejecuta de verdad) e historial de todas las evaluaciones. |
| 10. Operación de riego | Parcial | Riego manual con 2FA, motivo y duración; paro total; riego automático por reglas; control de válvulas por MQTT/ACK. | Programación de riegos futuros o recurrentes. |
| 11. Tanque y bombeo | Parcial | Bomba y tanque: tanques, nivel, encendido y apagado, protección por marcha en seco y sobrecorriente, historial de abastecimiento. | Llenado automático por niveles y gestión de fuentes de abastecimiento (hoy solo catálogo). |
| 12. Energía solar | Parcial | Monitoreo energético: generación, batería y consumo; protección con `MIN_AUTOMATION_BATTERY_PERCENT`. | Registro de paneles, baterías y controladores de carga desde la interfaz (hoy sembrados) e historial de autonomía. |
| 13. Consumo y eficiencia | Parcial | Consumo de agua: registro, consumo por zona, sector y cultivo, estimado frente a medido y costo. | Ahorro de agua frente a línea base y detección de consumo anormal. |
| 14. Mantenimiento | Parcial | Planes e incidencias: planes por equipo registrado, incidencias manuales y desde alertas, historial de actividades. | Separación preventivo/correctivo, órdenes de trabajo con técnico y fecha compromiso, historial por dispositivo en pantalla. |
| 15. Notificaciones y alertas | Parcial | Centro de alertas y campana de notificaciones: bandeja, reconocer y resolver, escalamiento y webhook. | Reglas de notificación, canales (correo, Telegram, Teams), resúmenes diario y semanal, historial de n8n. |
| 16. Reportería y analítica | Parcial | Reportes y exportación: PDF de consumo, riegos y mantenimiento; Excel, CSV y JSON; vistas SQL `vw_PowerBI_*`; exportación Excel/PDF en las listas. | Power BI (conexión, modelo y DAX) y PDF de lecturas, alertas, IoT y energía. |
| 17. Auditoría y configuración | Parcial | Auditoría paginada (accesos, dispositivos, riegos y operaciones críticas con antes/después e IP); Parámetros globales; Centro de control. | Versionado de reglas, configuración general (nombre, zona horaria, formato) y configuración de n8n y Power BI. |
| 18. Implementación y capacitación | Pendiente | Fuera del sistema: publicación, instalación física, carga inicial, documentación y capacitación. | Todo el módulo. |

## Cambios que cerraron la auditoría

- Configuración remota completa: cola persistente, despacho inmediato y por worker, reintentos, tópico MQTT específico, aplicación en simulador y ACK correlacionado por comando y nodo.
- Sesión reforzada: el refresh token se entrega como cookie HttpOnly/SameSite Strict, no se persiste en sessionStorage y el acceso se renueva automáticamente.
- Cobertura real del pipeline: pruebas HTTP con aplicación y middleware completos, además de las pruebas unitarias y de integración existentes.
- Rendimiento del frontend: carga diferida por módulo; el paquete inicial bajó de aproximadamente 1,359 kB a 407 kB.
- Consulta de datos: EF Core usa consultas divididas para evitar la advertencia de múltiples colecciones relacionadas.

## Validación final

- Backend: 202 de 202 pruebas .NET superadas.
- Frontend: typecheck, ESLint y build de producción superados.
- Simulador MQTT: compilación .NET 10 superada sin errores ni advertencias.
- Cronograma XLSX: reabierto después de exportar, fórmula ponderada recalculada y cero errores de fórmula.

La evidencia detallada de los primeros siete módulos permanece en `docs/trazabilidad-modulos-1-7.md`. Las instrucciones de operación local, seguridad, simuladores y validación visual se encuentran en `README.md`.
