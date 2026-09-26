# Pruebas por módulo

Baterías que se ejecutan contra la API en marcha, módulo por módulo. Comprueban lo que la hoja «Módulos detallados» pide de cada submódulo, contrastan los números contra la base de datos y revisan seguridad, entradas inválidas y reacción a cambios reales.

## Cómo ejecutarlas

1. Levanta la API contra una base de pruebas, nunca contra la de trabajo:

```bash
dotnet run --project backend/SistemaRiego.Api/SistemaRiego.Api.csproj --urls http://localhost:5080 -- "--ConnectionStrings:DefaultConnection=Server=(localdb)\MSSQLLocalDB;Database=SistemaRiego_QA;Trusted_Connection=True;TrustServerCertificate=True" --SeedAdmin:Password=TU_CLAVE_DE_PRUEBA --Seed:IncludeDemoData=true
```

2. Inicia sesión una vez. Si la cuenta tiene segundo factor, agrega `QA_TOTP_SECRET` con la clave compartida que muestra Seguridad 2FA:

```bash
QA_PASSWORD=TU_CLAVE_DE_PRUEBA python qa/sesion.py
```

3. Ejecuta el módulo que quieras revisar:

```bash
python qa/modulo01_inicio.py
```

El token queda en `qa/qa_state.json` (ignorado por git). Con `QA_DB` se indica otra base si no se usa `SistemaRiego_QA2`.

## Qué hay

| Archivo | Módulo | Comprobaciones |
|---|---|---|
| `modulo01_inicio.py` | 1 · Inicio | 38 |
| `modulo02_datos_maestros.py` | 2 · Datos maestros | 52 |
| `modulo03_seguridad.py` | 3 · Seguridad | 53 |

La evidencia de cada módulo, con capturas y resultados, está en `docs/evidencia/`.
