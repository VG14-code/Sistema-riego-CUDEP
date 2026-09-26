# Módulo 1 · Inicio

Cierre del módulo el 26 de septiembre de 2026. La referencia es la hoja «Módulos detallados» de `Planificacion final Victor Gabriel Madrid .xlsx`.

## Qué pedía la planificación y dónde quedó

| Submódulo | Tarea de la hoja | Dónde está | Estado |
|---|---|---|---|
| Resumen operativo | Concentrar sensores activos, zonas en riego, tanque, bomba, batería, consumo y alertas; tarjetas, semáforos, gráficas y acceso rápido a incidencias | `GET /api/system/dashboard`; pantalla en [Sprint1Modules.tsx](../../frontend/src/Sprint1Modules.tsx) | Completo |
| Mapa de la granja | Centro → finca → bloque → sector → zona, con filtros, colores por estado y detalle emergente | `GET /api/territory/hierarchy`; componente `FarmMap` con Leaflet | Completo |
| Monitoreo en tiempo real | Lecturas y cambios operativos mientras ocurren, con hora de última lectura y estado de conexión | SignalR en `/hubs/telemetry`; panel «Telemetría en vivo» | Completo |
| Actividad reciente | Últimas lecturas, riegos, alertas y cambios críticos, con filtros y enlaces al detalle | `recentActivity` del resumen; panel «Actividad consolidada» | Completo |

## Lo que se agregó en este cierre

1. **Semáforos en las tarjetas.** Cada indicador se colorea según su estado: verde normal, ámbar vigilar y rojo atender. Los umbrales son humedad 35/20 %, tanque 35/15 %, batería 40/25 % y alertas 1/5.
2. **Gráfica de tendencia.** Consumo de agua de los últimos catorce días, tomado de `GET /api/operations/summary`, con acceso directo al módulo 13. Si aún no hay riegos, la pantalla lo explica en vez de mostrar una gráfica vacía.
3. **Acceso rápido a incidencias.** Botón con el número de alertas activas que lleva al Centro de alertas (módulo 15) y se resalta en rojo cuando hay alertas.
4. **Filtros del mapa.** Por estado (todas, activas, requieren atención) y por sector, con el contador de zonas visibles.
5. **Filtros y enlaces en la actividad.** Fichas por origen (auditoría, telemetría, riego, comandos) con su conteo, y un enlace «Ver detalle» que abre el módulo donde se revisa cada evento.
6. **Arreglos de presentación.** Las tarjetas ya no desbordan la pantalla en 1024 px, la barra de la cabecera y los botones de los paneles se acomodan en varias líneas, y el saludo ya no se corta en teléfonos.

## Pruebas ejecutadas

**Funcionales y de validación:** `python qa/sesion.py && python qa/modulo01_inicio.py` → **38 de 38 comprobaciones superadas** contra la API en marcha y la base de pruebas.

| Grupo | Qué comprueba |
|---|---|
| Indicadores | Cada número del resumen se contrasta con una consulta SQL directa: sensores, zonas, alertas, zonas en riego, nivel del tanque y estado de la bomba |
| Tendencia | La serie diaria llega con litros por día |
| Mapa | Los cinco niveles, el conteo de zonas frente a la base, el estado de cada zona y las coordenadas |
| Actividad | Orden por fecha, mezcla de orígenes y campos completos para el enlace al detalle |
| Seguridad | Sin cabecera, con token inválido, con la firma alterada y sin el prefijo «Bearer»: 401 en los cuatro casos, en resumen, mapa y tendencia |
| Robustez | Identificador mal formado (400, no 500), tamaños negativos, cero y de un millón, y parámetros desconocidos |
| Reacción real | Cambio del nivel del tanque reflejado en el resumen; lectura nueva visible; duplicada rechazada (409); fuera de rango marcada inválida; sensor desconocido rechazado (400) |

**En el navegador:** inicio de sesión con segundo factor, filtro del mapa por sector (de 3 zonas a 1), filtro de actividad por telemetría (solo eventos de ese origen), «Ver detalle» que abre el módulo 5 y el botón de incidencias que abre el módulo 15. SignalR entregó eventos en vivo durante la prueba.

**Medidas:** el resumen responde entre 8 y 30 ms y el mapa por debajo de 1.5 s. Sin desbordamiento horizontal en 1440, 1024 ni 375 px.

## Capturas

| Vista | Archivo |
|---|---|
| Resumen operativo completo | [modulo-01-inicio-completo.png](img/modulo-01-inicio-completo.png) |
| Primera pantalla | [modulo-01-inicio-vista.png](img/modulo-01-inicio-vista.png) |
| Teléfono (375 px) | [modulo-01-inicio-movil.png](img/modulo-01-inicio-movil.png) |

Se regeneran con `node scripts/module-evidence.mjs "Inicio" "Resumen operativo" modulo-01-inicio` desde `frontend`.

## Correcciones previas relacionadas

Durante la auditoría de este módulo aparecieron dos defectos que ya están corregidos y con prueba propia: el nivel del tanque podía vaciarse con una petición incompleta y los riegos programados no respetaban el máximo de válvulas simultáneas.

## Pendiente

Nada del módulo 1. Los datos mostrados dependen de que haya nodos publicando: en la base de pruebas los indicadores de nodos y dispositivos en línea aparecen en rojo cuando el simulador está apagado, que es el comportamiento esperado.
