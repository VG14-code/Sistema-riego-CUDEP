# Estado de módulos — cierre funcional

Fecha de corte: 12 de septiembre de 2026.

## Resultado ejecutivo

Los módulos funcionales 1 a 10 están implementados y validados en el entorno local. El Módulo 11 queda parcial: los reportes PDF/Excel, las vistas SQL y el dashboard web funcionan, pero el modelo de Power BI no se ha construido y el dashboard carece de indicadores de humedad y temperatura, de la tendencia semanal en pantalla y de la comparación entre periodos. El Módulo 12 queda pendiente por decisión del proyecto, porque reúne publicación en Azure, secretos y certificados de producción, instalación física y puesta en marcha. El Módulo 13 no presenta una brecha de software: su ejecución presencial, entrega de credenciales institucionales y firma de aceptación dependen de que el despliegue haya concluido.

El cronograma oficial registra 111 actividades al 100 %, 6 al 50 % y 13 al 0 %. El avance ponderado por horas es 83.9 %.

- **Al 0 %:** seis actividades de despliegue y tres de capacitación/cierre, que dependen del despliegue; dos de montaje físico en campo, la fila 28 (protecciones, cajas y alimentación) y la fila 81 (sensores de nivel, relés y conexiones de la bomba), cuyo software está validado con el simulador MQTT; y las filas 113 y 114, conexión y modelo de Power BI, que no se han construido.
- **Al 50 %:** las filas 86 y 88, cuyas protecciones por software están probadas pero las eléctricas son físicas; la 115, que tiene las vistas probadas pero no el modelo de Power BI; y las filas 116, 117 y 119, a cuyo dashboard le faltan humedad, temperatura, tendencia semanal en pantalla y comparación entre periodos.
- **Adelantadas:** 34 actividades de los módulos 8 a 11 tienen fecha de inicio posterior al corte y siguen al 100 %, porque su código está en el repositorio desde el 17/08/2026. Conservan las fechas planificadas como línea base y lo indican en Observaciones.

## Estado por módulo

| Módulo | Estado | Evidencia principal |
|---|---|---|
| 1. Autenticación y roles | Completo | JWT, 2FA, recuperación, RBAC, sesiones y renovación segura mediante cookie HttpOnly. |
| 2. Datos maestros | Completo | Catálogos, parámetros globales, validaciones y administración desde la interfaz. |
| 3. Sensores y dispositivos IoT | Completo en software | Inventario, nodos, sensores, calibración, firmware, mantenimiento y trazabilidad. La instalación real se ejecuta en M12. |
| 4. Captura y registro de lecturas | Completo | REST/MQTT, validación, deduplicación, historial, agregaciones, calidad y SignalR. |
| 5. Cultivos y requerimientos hídricos | Completo | Cultivos, etapas, suelos, requerimientos, evaluación ambiental y recomendaciones explicables. |
| 6. Sectores y zonas de riego | Completo | Jerarquía territorial, asignaciones, mapas y resumen operativo. |
| 7. Automatización y reglas de riego | Completo | Reglas, ventanas, histéresis, seguridad, comandos MQTT, ACK y reconciliación. |
| 8. Bomba y abastecimiento del tanque | Completo en software | Control, protecciones, telemetría e historial validados con simulador. Montaje físico en M12. |
| 9. Riegos manuales y operación asistida | Completo | Autorización, TOTP, trazabilidad, pausa y reanudación del modo automático. |
| 10. Consumo e historial operativo | Completo | Cálculo medido/estimado, consolidados, bitácora, filtros y exportación. |
| 11. Reportes, dashboards y análisis | Parcial | PDF/Excel, dashboard web, vistas SQL y procedimiento de origen. Pendiente: modelo Power BI (.pbix y DAX), indicadores de humedad y temperatura, tendencia semanal en pantalla y comparación entre periodos. |
| 12. Despliegue e implementación | Pendiente | Azure, CI/CD, certificados, secretos, instalación física, migración y publicación. |
| 13. Capacitación y entrega | Dependiente de M12 | Material y documentación disponibles; capacitación, entrega institucional y aceptación se realizan después del despliegue. |

## Cambios que cerraron la auditoría

- Configuración remota completa: cola persistente, despacho inmediato y por worker, reintentos, tópico MQTT específico, aplicación en simulador y ACK correlacionado por comando y nodo.
- Sesión reforzada: el refresh token se entrega como cookie HttpOnly/SameSite Strict, no se persiste en sessionStorage y el acceso se renueva automáticamente.
- Cobertura real del pipeline: pruebas HTTP con aplicación y middleware completos, además de las pruebas unitarias y de integración existentes.
- Rendimiento del frontend: carga diferida por módulo; el paquete inicial bajó de aproximadamente 1,359 kB a 407 kB.
- Consulta de datos: EF Core usa consultas divididas para evitar la advertencia de múltiples colecciones relacionadas.

## Validación final

- Backend: 173 de 173 pruebas .NET superadas.
- Frontend: typecheck, ESLint y build de producción superados.
- Simulador MQTT: compilación .NET 10 superada sin errores ni advertencias.
- Cronograma XLSX: reabierto después de exportar, fórmula ponderada recalculada y cero errores de fórmula.

La evidencia detallada de los primeros siete módulos permanece en `docs/trazabilidad-modulos-1-7.md`. Las instrucciones de operación local, seguridad, simuladores y validación visual se encuentran en `README.md`.
