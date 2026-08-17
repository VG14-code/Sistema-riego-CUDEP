# Guía de cambios ? semanas 11 a 14

## Resultado

El sistema alcanza el avance de software previsto hasta la semana 14 de la planificación. Las semanas 11 a 13 consolidan los módulos de bomba y tanque, riego manual, consumo y bitácora. La semana 14 completa la consulta y exportación operativa e inicia el modelo analítico para Power BI.

> El hardware físico sigue siendo trabajo de campo: sensores de nivel, relés, protecciones eléctricas, parada de emergencia, bomba y válvulas deben ser instalados y validados por personal calificado. El panel permite simulación persistida, pero no afirma que esos equipos ya están conectados.

## Avance por semana

| Semana | Entrega de software | Estado |
|---|---|---|
| 11 | Tanque, bomba, nivel, encendido, apagado, límites, descansos, bloqueos e indicadores | Completado |
| 12 | Historial de abastecimiento, riego manual autorizado, duración, caudal, motivo, responsable y observaciones | Completado |
| 13 | Pausa segura del modo automático, consumo por evento, consolidaciones y bitácora | Completado |
| 14 | Filtros por fecha, categoría, zona, usuario y texto; detalle; CSV/JSON; modelo SQL para Power BI | Completado en software |

## Cambios visibles

1. Inicia sesión y abre **Consumo**.
2. La pantalla ahora se identifica como **Semana 14 ? Modelo analítico**.
3. Selecciona un periodo de 7, 30, 90 o 365 días.
4. Revisa consumo total, eventos, promedio, tendencia diaria y consolidación por sector.
5. Usa los filtros de fecha, categoría, zona, usuario o texto.
6. Pulsa un evento para abrir su detalle completo.
7. Exporta el resultado filtrado en **CSV** o **JSON**.

## Backend agregado

Controlador: `Week14AnalyticsController`.

- `GET /api/week14/dashboard?days=30`: consolidación diaria, semanal, por zona y por sector.
- `GET /api/week14/filters`: opciones existentes de categoría, zona y usuario.
- `GET /api/week14/history`: historial con filtros combinables.
- `GET /api/week14/history/{id}`: detalle de un evento.
- `GET /api/week14/history.csv`: exportación CSV con los filtros activos.
- `GET /api/week14/history.json`: exportación JSON con los filtros activos.
- `GET /api/week14/powerbi/model`: metadatos del modelo; disponible para Administrador.

## Base de datos y Power BI

La migración `Week14AnalyticsViews` crea:

- `dbo.vw_PowerBI_Consumption`: consumo enlazado con zona, sector, lote y finca.
- `dbo.vw_PowerBI_Telemetry`: lecturas enlazadas con sensor, zona y sector.
- `dbo.vw_PowerBI_OperationalEvents`: bitácora enlazada con zona y sector.
- `dbo.sp_PowerBI_ConsumptionSummary`: resumen por fecha, sector y zona.

### Conectar Power BI Desktop

1. Asegúrate de haber iniciado el sistema al menos una vez.
2. Abre Power BI Desktop.
3. Selecciona **Obtener datos ? SQL Server**.
4. Servidor: `(localdb)\MSSQLLocalDB`.
5. Base de datos: `SistemaRiego`.
6. Selecciona modo **Importar**.
7. Marca las tres vistas cuyo nombre inicia con `vw_PowerBI_`.
8. Crea una tabla calendario y relaciónala con `ConsumptionDate`, `ReadingDate` y `EventDate`.
9. Agrega estas medidas DAX:

```DAX
Litros Totales = SUM(vw_PowerBI_Consumption[VolumeLiters])
Promedio por Riego = AVERAGE(vw_PowerBI_Consumption[VolumeLiters])
Eventos de Riego = COUNTROWS(vw_PowerBI_Consumption)
Lecturas Válidas = CALCULATE(COUNTROWS(vw_PowerBI_Telemetry), vw_PowerBI_Telemetry[IsValid] = TRUE())
```

10. Construye tarjetas de litros y eventos, una tendencia por fecha, barras por sector y una tabla de fallas/eventos.

## Levantar y demostrar

```powershell
.\Iniciar Sistema.ps1
```

Abre `http://127.0.0.1:5173/` e ingresa con:

```text
Correo: admin@sistemariego.local
Contraseña: [configurada mediante User Secrets]
```

## Validaciones

- Compilación del backend real sin errores ni advertencias.
- 19 pruebas automatizadas del backend superadas.
- ESLint sin errores.
- Compilación de producción de Vite superada.
- Migración aplicada en SQL Server LocalDB.
- Vistas y procedimiento analítico creados.

## Pendiente físico

- Instalar sensor de nivel y calibrarlo con el tanque real.
- Cablear rel?, protecciones, bomba y parada de emergencia.
- Confirmar físicamente apertura y cierre de válvulas.
- Reemplazar caudal estimado con el caudalímetro real cuando est? disponible.
- Instalar Power BI Desktop y publicar el informe cuando se defina el espacio de trabajo institucional.


## Corrección ortográfica y datos para dashboards

Se corrigieron los caracteres dañados del encabezado de Semana 14, las opciones de período, el contador de filtros, el valor sin fecha y el botón de cierre.

El sembrador `EmpiricalDashboardSeeder` agrega una muestra reproducible de 84 días para pruebas visuales y analíticas:

- 3 sectores y 3 zonas de riego.
- 99 eventos de consumo disponibles en el período de 90 días.
- 13 semanas con variación de duración, caudal, volumen y humedad.
- 5 categorías de bitácora, incluyendo riego, mantenimiento y alertas.
- Telemetría diaria con humedad, batería y señal variables.

Los registros están marcados como **muestra calibrada de prueba**. Son datos sintéticos reproducibles basados en rangos plausibles de campo y no deben presentarse como mediciones obtenidas de sensores físicos conectados.
