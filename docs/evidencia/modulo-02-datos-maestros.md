# Módulo 2 · Datos maestros

Cierre del módulo el 26 de septiembre de 2026. La referencia es la hoja «Módulos detallados» de `Planificacion final Victor Gabriel Madrid .xlsx`.

## Qué pedía la planificación y dónde quedó

| Submódulo | Dónde está en el sistema | Estado |
|---|---|---|
| Centros universitarios | Datos maestros → Centros, fincas y zonas, pestaña Centro | Completo |
| Fincas | Misma pantalla, pestaña Finca, ligada a su centro | Completo |
| Bloques | Pestaña Bloque, con finca, tipo de suelo y área | Completo |
| Sectores | Pestaña Sector, con área y pendiente | Completo |
| Zonas de riego | Pestaña Zona, con sector, estado, sensores y válvulas | Completo |
| Tipos de suelo | Datos maestros → Suelos, cultivos y etapas | Completo |
| Tipos de cultivo | Catálogos → Tipos de cultivo | Completo |
| Cultivos | Suelos, cultivos y etapas | Completo |
| Etapas fenológicas | Suelos, cultivos y etapas, con su orden | Completo |
| Unidades de medida | Catálogos → Unidades, con unidad base, factor y conversor | Completo |
| Tipos de sensores | Catálogos → Tipos de sensor | Completo |
| Tipos de dispositivos IoT | Catálogos → Tipos de dispositivo | Completo |
| Marcas y modelos | Catálogos → Marcas y Modelos, con precisión, voltaje y protocolo | Completo |
| Tipos de válvula | Catálogos → Tipos de válvula | Completo |
| Tipos de bomba | Catálogos → Tipos de bomba | Completo |
| Fuentes de agua | Catálogos → Fuentes de agua | Completo |
| Tipos de alerta | Catálogos → Tipos de alerta | Completo |
| Estados operativos | Catálogos → Estados | Completo |
| Motivos de suspensión | Catálogos → Motivos de suspensión | Completo |
| Frecuencias de lectura | Catálogos → Frecuencias de lectura, con intervalo en segundos | Completo |

## Lo que se agregó en este cierre

1. **Búsqueda, paginación y exportación en el territorio.** La lista de centros, fincas, bloques, sectores y zonas tenía todos los registros de corrido. Ahora usa la misma barra que el resto del sistema: busca por código, nombre, ubicación o estado, pagina de ocho en ocho y exporta a Excel y PDF lo que quedó filtrado. Era la observación de la hoja para fincas.
2. **Acceso desde el menú del módulo.** La hoja ubica los tipos de suelo, los cultivos y las etapas fenológicas en Datos maestros, pero solo se llegaba a ellos desde Gestión agronómica. Se agregó la entrada «Suelos, cultivos y etapas» en el módulo 2, que abre la misma pantalla.
3. **El intervalo visible en frecuencias de lectura.** La tabla mostraba solo código y nombre; ahora indica «Intervalo 60 s», igual que las unidades muestran su base y su factor. La búsqueda también considera esos datos.
4. **Los catálogos abren en la primera pestaña.** Antes entraban en «Tipos de válvula», la quinta de la lista.

## Pruebas ejecutadas

**Funcionales y de validación:** `python qa/sesion.py && python qa/modulo02_datos_maestros.py` → **52 de 52 comprobaciones superadas**.

| Grupo | Qué comprueba |
|---|---|
| Jerarquía | Alta de centro, finca, bloque, sector y zona encadenados, y su aparición completa en la jerarquía con el tipo de suelo del bloque |
| Rechazos | Código repetido de centro y de sector (409), finca con centro inexistente, bloque con área cero, cultivo con tipo inexistente, etapas con secuencia repetida o duración cero |
| Edición y baja lógica | Editar un centro y desactivar una zona, que sigue en el listado para reactivarla |
| Catálogos agronómicos | Alta de tipo de suelo con sus parámetros, rechazo de saturación menor que la capacidad de campo, tipos de cultivo, cultivos y etapas |
| Unidades | Alta con unidad base y factor, conversión de 2500 mL a 2.5 L, rechazo entre magnitudes distintas y de factor cero |
| Catálogos técnicos | Los nueve catálogos responden con registros; alta, edición, código repetido (409) y tipo de catálogo inexistente (400); frecuencia con y sin intervalo |
| Marcas y modelos | Alta de marca y modelo con precisión, voltaje y protocolo, visibles en el listado; rechazo de modelo con marca inexistente |
| Integridad | No se borra un tipo de cultivo con cultivos ni una marca con modelos |
| Seguridad y exportación | Jerarquía y catálogos exigen sesión válida; la exportación entrega un Excel y un PDF válidos |

**En el navegador:** búsqueda en el territorio («CUDEP» dejó 1 de 4 registros y un texto inexistente mostró «Sin coincidencias»), exportación a Excel desde la pantalla (`POST /api/exports/xlsx` → 200), las trece pestañas de catálogos, el conversor de unidades y el intervalo visible en frecuencias. Sin desbordamiento horizontal en 1440 px.

## Capturas

| Vista | Archivo |
|---|---|
| Territorio (centros, fincas, bloques, sectores, zonas) | [modulo-02-territorio-vista.png](img/modulo-02-territorio-vista.png) · [completo](img/modulo-02-territorio-completo.png) · [teléfono](img/modulo-02-territorio-movil.png) |
| Catálogos | [modulo-02-catalogos-vista.png](img/modulo-02-catalogos-vista.png) · [completo](img/modulo-02-catalogos-completo.png) · [teléfono](img/modulo-02-catalogos-movil.png) |
| Suelos, cultivos y etapas | [modulo-02-agronomia-vista.png](img/modulo-02-agronomia-vista.png) · [completo](img/modulo-02-agronomia-completo.png) · [teléfono](img/modulo-02-agronomia-movil.png) |

Se regeneran desde `frontend` con `node scripts/module-evidence.mjs "Datos maestros" "Catálogos" modulo-02-catalogos`.

## Pendiente

Nada del módulo 2. La pantalla de suelos, cultivos y etapas es compartida con el módulo 6: desde Datos maestros se administra el catálogo y desde Gestión agronómica se trabajan los requerimientos de riego.
