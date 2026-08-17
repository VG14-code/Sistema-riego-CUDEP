# Guía de adaptación a la nueva planificación — módulos 1 al 7

## Resultado general

El sistema pasó de ser una administración básica de seguridad, catálogos e IoT a un prototipo funcional que conecta la estructura física de la finca con sensores, lecturas, criterios agronómicos y ciclos de cultivo. El flujo demostrable ahora es:

**Finca y zona → sensor instalado → lectura validada → requerimiento hídrico → recomendación explicada → seguimiento del cultivo.**

La ampliación conserva la base tecnológica existente (ASP.NET Core 9, Entity Framework Core, SQL Server, JWT, React, Vite y GSAP) para no romper el trabajo previo. La hoja propone versiones y componentes futuros, como .NET 10, MQTT, SignalR y 2FA; estos se consideran pasos de endurecimiento o integración física, no se simulan como terminados.

## Correspondencia con la planificación

| Módulo | Punto de la planificación | Adaptación realizada | Cómo se demuestra |
|---|---|---|---|
| 1. Inicio | Resumen, mapa jerárquico, monitoreo y actividad reciente | Tablero con humedad media, sensores, zonas, ciclos, calidad, gráfica, mapa jerárquico y auditoría | Abrir **Inicio** y pulsar **Actualizar ahora** |
| 2. Datos maestros | Centros, fincas, bloques, sectores, zonas, suelos, cultivos y catálogos técnicos | Modelo territorial completo y modelos agronómicos. Se conservan los catálogos de sensores, dispositivos, unidades y estados | Abrir **Finca**, **Catálogos** y **Administración** |
| 3. Seguridad | Usuarios, roles, permisos, sesiones y autenticación | JWT con renovación, roles Administrador/Técnico/Operador, bloqueo, recuperación, auditoría y consulta/revocación de sesiones | Ingresar con perfiles distintos; revisar **Usuarios** y **Auditoría** |
| 4. IoT | Dispositivos, instalación, comunicación, conexión, configuración, firmware e inventario | Red IoT existente con nodos, dispositivos, sensores, estado, ubicación, protocolo, firmware, calibraciones y comandos remotos | Abrir **Red IoT** y recorrer nodos, dispositivos y sensores |
| 5. Sensores y lecturas | Recepción, tiempo real, historial, validación, calibración y calidad | Endpoint de recepción, rechazo de duplicados, validación de rango/batería/saltos, historial, calidad, simulador y gráfica | Abrir **Lecturas** y pulsar **Generar lectura** |
| 6. Agronomía | Requerimientos por cultivo/etapa, suelo, ambiente y recomendación explicable | Suelos, cultivos, etapas y reglas de humedad/volumen/frecuencia; recomendador compara lectura real con umbrales | Abrir **Agronomía** y revisar la explicación de la decisión |
| 7. Planificación de cultivos | Ciclos, zona, etapa, rotación y calendario agrícola | Ciclo asignado a zona, etapa actual, siembra, cosecha esperada, área, plantas, estado y progreso | Abrir **Cultivos** y revisar calendario/progreso |

## Cambios técnicos

### Base de datos

Se agregaron entidades para centros universitarios, fincas, bloques, sectores, zonas, tipos de suelo, lecturas, comandos IoT, tipos de cultivo, cultivos, etapas fenológicas, requerimientos hídricos y ciclos. La migración `Modules1To7Expansion` crea tablas, claves foráneas, índices únicos y precisión decimal apropiada.

Los datos iniciales forman una demostración coherente: CUDEP → Granja experimental → Bloque A → Sector norte → Zona tomate A1. La zona está relacionada con el sensor de humedad, el cultivo de tomate, cuatro etapas, reglas hídricas y un ciclo activo. También se cargan lecturas históricas de humedad y temperatura.

### Backend y API

- `TerritoryController`: jerarquía y mantenimiento territorial.
- `TelemetryController`: recepción, historial, calidad, simulación y comandos.
- `AgronomyController`: suelos, cultivos, etapas, requerimientos y recomendaciones.
- `CropPlanningController`: ciclos, calendario, edición y cambio de etapa.
- `SystemDashboardController`: indicadores operativos consolidados.
- `SessionsController`: sesiones visibles sin exponer tokens y revocación remota.

Las recomendaciones no son una “caja negra”: indican humedad actual, umbral mínimo, objetivo, minutos, litros y la razón de la decisión. El operador sigue teniendo la última decisión.

### Frontend

La navegación ahora incorpora **Inicio**, **Finca**, **Lecturas**, **Agronomía** y **Cultivos**, sin quitar Usuarios, Catálogos, Red IoT, Parámetros ni Auditoría. El diseño usa azules y verdes para representar agua, conectividad y cultivo.

Se añadieron tarjetas reactivas, gráficas SVG, jerarquía territorial, estados de sensores, progreso de ciclos, indicadores de calidad y mensajes claros. GSAP anima la entrada escalonada de tarjetas, parallax, profundidad y escenas de datos; si el equipo tiene activado “reducir movimiento”, las animaciones se deshabilitan por accesibilidad.

## Guía para la exposición

1. Ejecutar `Iniciar Sistema.ps1` desde la carpeta raíz.
2. Abrir `http://127.0.0.1:5173/`.
3. Ingresar con `admin@sistemariego.local` y `[configurada mediante User Secrets]`.
4. En **Inicio**, explicar que todos los indicadores vienen de la API y la base de datos.
5. En **Finca**, recorrer la jerarquía hasta la zona y mostrar el sensor asignado.
6. En **Red IoT**, mostrar inventario, estado, firmware y calibraciones.
7. En **Lecturas**, pulsar **Generar lectura**; explicar validación, historial y calidad.
8. En **Agronomía**, comparar humedad actual con mínimos y objetivos; leer la explicación del sistema.
9. En **Cultivos**, mostrar ciclo, etapa, zona, fechas, área, plantas y avance.
10. En **Auditoría**, mostrar que las acciones importantes quedan registradas.

## Perfiles de prueba

| Perfil | Correo | Contraseña | Uso sugerido |
|---|---|---|---|
| Administrador | `admin@sistemariego.local` | `[configurada mediante User Secrets]` | Configuración y exposición completa |
| Técnico | `tecnico@sistemariego.local` | `Tecnico123!` | IoT, telemetría y mantenimiento |
| Operador | `operador@sistemariego.local` | `Operador123!` | Consulta y operación cotidiana |

## Alcance real y siguientes integraciones

Hasta el módulo 7 el software queda preparado y demostrable con simulación persistida. La simulación utiliza exactamente el mismo modelo de datos y validación que recibirá el hardware; no pretende afirmar que un sensor físico está conectado.

Para producción faltaría conectar ESP32/Raspberry Pi mediante MQTT, publicar actualizaciones con SignalR, activar 2FA con un proveedor real, incorporar cartografía GIS completa, ejecutar ensayos agronómicos para calibrar umbrales y desplegar en infraestructura institucional. Rotación de cultivos está representada por ciclos históricos de una zona; una regla avanzada de incompatibilidades pertenece a una iteración posterior.

## Verificación realizada

- Compilación del frontend y revisión ESLint.
- Compilación del backend y migración de Entity Framework.
- Suite automatizada existente del backend: 16 pruebas superadas.
- Validación de endpoints con autenticación y datos persistidos.
