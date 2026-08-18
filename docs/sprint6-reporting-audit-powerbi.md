# Sprint 6 — Reportes, auditoría y Power BI

## Reportes operativos

Todos los endpoints requieren JWT y aceptan filtros de período (from, to). Los reportes de consumo y riego aceptan zoneId; consumo también acepta crop. Mantenimiento acepta equipmentType y equipmentId.

- GET /api/reports/consumption.pdf: consumo y eficiencia por zona/cultivo, recomendación, desviación y costo.
- GET /api/reports/irrigation.pdf: riegos manuales/automáticos, comando MQTT y estado del ACK.
- GET /api/reports/maintenance.pdf: planes, actividades e incidencias por equipo.
- GET /api/reports/analytics.xlsx: hojas Consumo, Riegos, Mantenimiento y Totales, con filtros, encabezados y formatos.

QuestPDF usa la licencia Community. ClosedXML genera los libros sin depender de Excel instalado.

## Auditoría sensible y retención

CorrelationIdMiddleware conserva X-Correlation-ID cuando llega en la petición o genera uno nuevo, lo devuelve en la respuesta y lo agrega al contexto de Serilog.

El interceptor de EF registra creación, actualización y eliminación de usuarios, roles/permisos, catálogos, parámetros, reglas/riegos, comandos/ACK, alertas y mantenimiento. En actualizaciones almacena JSON antes/después; contraseñas, tokens, autenticadores, códigos de recuperación y stamps siempre se reemplazan por [PROTEGIDO].

La auditoría tiene tabla y política de retención independientes. AuditRetentionService busca el parámetro AUDIT_RETENTION_DAYS; si no existe usa 730 días, con límites de 30 a 3650 días, y depura diariamente. La consulta y las exportaciones filtradas están en:

- GET /api/audit-trail
- GET /api/audit-trail/filters
- GET /api/audit-trail/export.csv
- GET /api/audit-trail/export.xlsx

Filtros: from, to, user, action y entity.

## Power BI Desktop

### Estado del entorno

Power BI Desktop no está instalado en la máquina de validación del Sprint 6. Por ello no fue posible producir ni abrir un archivo .pbix real. La API y las vistas SQL permanecen listas para crear el archivo en una estación Windows que sí tenga Power BI Desktop.

### Construcción reproducible del archivo .pbix

1. Abrir Power BI Desktop y elegir **Obtener datos → SQL Server**.
2. Servidor local: (localdb)\MSSQLLocalDB; base: SistemaRiego. En staging/producción sustituir por el servidor configurado.
3. Elegir modo **Importar** y cargar:
   - vw_PowerBI_Consumption
   - vw_PowerBI_Telemetry
   - vw_PowerBI_OperationalEvents
4. En la vista Modelo validar la jerarquía Zona → Sector → Lote → Finca y las relaciones Riego → Consumo y Sensor → Telemetría → Zona.
5. Crear estas medidas:

   ~~~DAX
   Litros Totales = SUM(vw_PowerBI_Consumption[VolumeLiters])
   Promedio por Riego = AVERAGE(vw_PowerBI_Consumption[VolumeLiters])
   Eventos de Riego = COUNTROWS(vw_PowerBI_Consumption)
   ~~~

6. Crear una página **Eficiencia hídrica** con:
   - tarjeta Litros Totales;
   - gráfico de columnas VolumeLiters por zona, con cultivo como leyenda;
   - serie temporal diaria de VolumeLiters, acompañada por segmentadores de fecha, zona y cultivo.
7. Crear una página **Operación y mantenimiento** con una matriz de eventos por categoría/severidad y segmentadores de usuario y período.
8. Guardar como SistemaRiego-Sprint6.pbix y actualizar los parámetros del origen antes de publicar.

El endpoint administrativo GET /api/week14/powerbi/model expone nombres de vistas, relaciones y medidas para verificar el modelo.
