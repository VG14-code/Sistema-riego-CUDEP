# Guía de cambios — módulos 7 al 10

## Resultado general

El Sistema de Riego ahora completa el flujo operativo desde la lectura de humedad hasta el consumo registrado:

**Sensor → regla automática → riego autorizado → tanque y bomba → consumo → bitácora exportable.**

Los módulos utilizan ASP.NET Core 9, Entity Framework Core, SQL Server LocalDB, React y Vite. Las operaciones están persistidas en la base de datos y protegidas por los roles existentes.

> Alcance real: los controles funcionan como un prototipo persistido y demostrable. La conexión eléctrica de relés, bomba, válvulas y sensores de nivel continúa siendo una actividad de infraestructura IoT; el software no afirma que esos equipos físicos estén conectados.

## Correspondencia con la planificación

| Módulo | Funcionalidad implementada | Pantalla |
|---|---|---|
| 7. Automatización y reglas | Umbrales de humedad, objetivo, histéresis, prioridad, ventana horaria, duración máxima, pausa por intervención manual y evaluación explicable | **Automatización** |
| 8. Bomba y tanque | Nivel y capacidad, límites seguros, encendido/detención, descanso mínimo, bloqueo, historial y simulación del sensor de nivel | **Bomba y tanque** |
| 9. Riego manual | Activación por zona, duración, caudal, motivo, observaciones, detención, responsable y pausa temporal de reglas automáticas | **Riego manual** |
| 10. Consumo e historial | Volumen por evento, consolidación por período y zona, bitácora filtrable y exportación CSV | **Consumo** |

## Cambios en la base de datos

La migración `Modules7To10Operations` agrega:

- `IrrigationRules`: reglas, umbrales, prioridades, ventanas y estado de evaluación.
- `IrrigationRuns`: ejecuciones automáticas o manuales, duración, caudal, responsable y estado.
- `WaterTanks`: capacidad, nivel actual, mínimos, máximos y estado.
- `WaterPumps`: estado de bomba, tiempo máximo, descanso, bloqueo y fallas.
- `WaterSupplyEvents`: ciclos de abastecimiento y litros suministrados.
- `WaterConsumptionRecords`: consumo por riego, caudal, duración, volumen y fuente.
- `OperationalEvents`: bitácora unificada de riegos, abastecimiento, alertas y cambios.

El inicializador crea una demostración coherente con la zona Tomate A1: una regla automática, un tanque de 10,000 L, una bomba y consumos históricos.

## Cambios en el backend

### Automatización

Controlador: `AutomationController`

- `GET /api/automation/rules`: consulta las reglas.
- `POST /api/automation/rules`: crea una regla.
- `PATCH /api/automation/rules/{id}/toggle`: activa o desactiva.
- `POST /api/automation/evaluate`: compara humedad, horario, pausas y riegos activos.
- `GET /api/automation/active`: consulta ejecuciones en curso.

La evaluación registra una explicación. No inicia un riego cuando está fuera de horario, existe una pausa manual, ya hay un riego activo o no existe una lectura válida.

### Bomba y tanque

Controlador: `WaterSupplyController`

- `GET /api/water-supply/status`: nivel, capacidad, estado y bombas.
- `POST /api/water-supply/tanks/{id}/level`: actualiza o simula una lectura de nivel.
- `POST /api/water-supply/pumps/{id}/start`: enciende con validaciones.
- `POST /api/water-supply/pumps/{id}/stop`: detiene y registra abastecimiento.
- `GET /api/water-supply/history`: consulta ciclos de llenado.

Protecciones: nivel máximo, falla, bloqueo temporal, descanso mínimo y operación duplicada.

### Riego manual

Controlador: `ManualIrrigationController`

- `GET /api/manual-irrigation/zones`: zonas y disponibilidad.
- `GET /api/manual-irrigation/runs`: historial de intervenciones.
- `POST /api/manual-irrigation/start`: inicia una operación autorizada.
- `POST /api/manual-irrigation/{id}/stop`: detiene, descuenta agua y registra consumo.

Al iniciar un riego manual, las reglas de esa zona quedan suspendidas durante la operación y diez minutos adicionales.

### Consumo y bitácora

Controlador: `OperationsController`

- `GET /api/operations/summary?days=30`: total, promedio, tendencia y distribución por zona.
- `GET /api/operations/history`: filtra por fecha, tipo, zona y texto.
- `GET /api/operations/export.csv`: descarga hasta 5,000 consumos en CSV.

## Cambios en el frontend

La navegación incorpora cuatro accesos:

1. **Automatización**: tarjetas de reglas, umbrales y última decisión.
2. **Bomba y tanque**: indicador de nivel, controles y ciclos de abastecimiento.
3. **Riego manual**: formulario de intervención, estimación y detención segura.
4. **Consumo**: indicadores, barras diarias, distribución por zona, búsqueda y CSV.

El menú lateral ahora tiene desplazamiento interno para que todos los módulos sean accesibles en pantallas pequeñas.

## Guía de demostración

### 1. Levantar el sistema

Desde la carpeta raíz puedes ejecutar:

```powershell
.\Iniciar Sistema.ps1
```

O iniciar manualmente el backend y frontend en terminales distintas.

### 2. Iniciar sesión

```text
Correo: admin@sistemariego.local
Contraseña: [configurada mediante User Secrets]
```

### 3. Probar el módulo 7

1. Abre **Automatización**.
2. Revisa humedad mínima, objetivo, histéresis, horario y duración máxima.
3. Pulsa **Evaluar ahora**.
4. Explica la decisión y el motivo mostrado por la regla.
5. Activa o desactiva la regla con el interruptor.

### 4. Probar el módulo 8

1. Abre **Bomba y tanque**.
2. Revisa litros actuales y porcentaje disponible.
3. Pulsa **Simular +500 L** para representar una lectura del sensor de nivel.
4. Enciende la bomba y luego detenla.
5. Revisa el ciclo registrado en el historial.

### 5. Probar el módulo 9

1. Abre **Riego manual**.
2. Selecciona la zona.
3. Define duración, caudal, motivo y observaciones.
4. Comprueba el consumo estimado antes de iniciar.
5. Pulsa **Confirmar e iniciar**.
6. Verifica que el riego aparezca como activo.
7. Pulsa **Detener** para generar el consumo y la bitácora.

### 6. Probar el módulo 10

1. Abre **Consumo**.
2. Cambia entre 7, 30 y 90 días.
3. Revisa total, promedio, eventos, tendencia y consumo por zona.
4. Busca un término en la bitácora.
5. Pulsa **Descargar CSV**.

## Validación realizada

- Backend compilado sin advertencias ni errores.
- Migración aplicada correctamente a `SistemaRiego` en LocalDB.
- 19 pruebas automatizadas superadas.
- Frontend compilado con Vite.
- ESLint sin errores.
- Smoke test autenticado: reglas, evaluación, tanque, zonas, riego manual, consumo, bitácora y CSV.

El smoke test puede repetirse con:

```powershell
.\scripts\smoke-modules7to10.ps1
```

El backend debe estar ejecutándose en `http://localhost:5080`.

## Trabajo físico pendiente

- Instalar y cablear sensor de nivel, relé, bomba y parada de emergencia.
- Programar la recepción de comandos en Raspberry Pi o microcontrolador.
- Confirmar aperturas y cierres reales de válvulas.
- Sustituir caudal estimado por lecturas de un caudalímetro cuando exista.
- Calibrar capacidades, caudales, intervalos y límites con pruebas de campo.
