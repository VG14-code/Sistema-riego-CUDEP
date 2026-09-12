# Estado de módulos — cierre funcional

Fecha de corte: 12 de septiembre de 2026.

## Resultado ejecutivo

Los once módulos funcionales del sistema están implementados y validados en el entorno local. El Módulo 12 queda pendiente por decisión del proyecto, porque reúne publicación en Azure, secretos y certificados de producción, instalación física y puesta en marcha. El Módulo 13 no presenta una brecha de software: su ejecución presencial, entrega de credenciales institucionales y firma de aceptación dependen de que el despliegue haya concluido.

El cronograma oficial registra 119 actividades funcionales al 100 %, seis actividades de despliegue al 0 % y tres actividades de capacitación/cierre al 0 % por su dependencia del despliegue. Otras dos actividades quedan al 0 % porque son montaje físico en campo: la fila 28 (protecciones, cajas y alimentación) y la fila 81 (sensores de nivel, relés y conexiones de la bomba); su software está validado con el simulador MQTT, pero la instalación no se ha realizado. El avance ponderado por horas es 88.2 %.

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
| 11. Reportes, dashboards y análisis | Completo en software | PDF/Excel, dashboards web, vistas SQL y modelo analítico reproducible. Publicación productiva en M12. |
| 12. Despliegue e implementación | Pendiente | Azure, CI/CD, certificados, secretos, instalación física, migración y publicación. |
| 13. Capacitación y entrega | Dependiente de M12 | Material y documentación disponibles; capacitación, entrega institucional y aceptación se realizan después del despliegue. |

## Cambios que cerraron la auditoría

- Configuración remota completa: cola persistente, despacho inmediato y por worker, reintentos, tópico MQTT específico, aplicación en simulador y ACK correlacionado por comando y nodo.
- Sesión reforzada: el refresh token se entrega como cookie HttpOnly/SameSite Strict, no se persiste en sessionStorage y el acceso se renueva automáticamente.
- Cobertura real del pipeline: pruebas HTTP con aplicación y middleware completos, además de las pruebas unitarias y de integración existentes.
- Rendimiento del frontend: carga diferida por módulo; el paquete inicial bajó de aproximadamente 1,359 kB a 407 kB.
- Consulta de datos: EF Core usa consultas divididas para evitar la advertencia de múltiples colecciones relacionadas.

## Validación final

- Backend: 170 de 170 pruebas .NET superadas.
- Frontend: typecheck, ESLint y build de producción superados.
- Simulador MQTT: compilación .NET 10 superada sin errores ni advertencias.
- Cronograma XLSX: reabierto después de exportar, fórmula ponderada recalculada y cero errores de fórmula.

La evidencia detallada de los primeros siete módulos permanece en `docs/trazabilidad-modulos-1-7.md`. Las instrucciones de operación local, seguridad, simuladores y validación visual se encuentran en `README.md`.
