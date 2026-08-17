# Guía de gestión territorial

## Cambio realizado

La pantalla **Finca** ahora cumple la descripción del módulo: permite registrar, modificar, activar y desactivar centros, fincas, bloques, sectores y zonas. También presenta la relación completa **Centro → Finca → Bloque → Sector → Zona**, junto con el sensor principal de cada zona.

## Roles

- **Administrador y Técnico:** pueden crear, modificar, activar y desactivar registros.
- **Operador:** tiene acceso de consulta y ve un aviso explicando la restricción.

## Cómo registrar la estructura

1. Inicia sesión y abre **Finca**.
2. En **Gestión territorial**, selecciona la pestaña **Centro**.
3. Ingresa código, nombre, ubicación y contacto.
4. Pulsa **Guardar centro**.
5. Selecciona **Finca**, elige el centro padre y completa sus datos.
6. Selecciona **Bloque**, elige la finca, el tipo de suelo y el área.
7. Selecciona **Sector**, elige el bloque, el área y la pendiente.
8. Selecciona **Zona**, elige el sector, estado operativo, sensor principal, área y coordenadas.
9. Pulsa el botón de guardado en cada nivel.

Los selectores solo muestran registros padre activos. Por esta razón, la estructura debe crearse en ese orden.

## Activar o desactivar

## Modificar un registro existente

1. En **Gestión territorial**, selecciona la pestaña del nivel que quieres modificar: **Centro**, **Finca**, **Bloque**, **Sector** o **Zona**.
2. Busca el elemento en la columna **Registros existentes**.
3. Pulsa **Editar**. El formulario de la izquierda cambiará a **Modificar** y cargará los datos actuales.
4. Cambia los campos necesarios. También puedes cambiar la relación padre, por ejemplo, mover una zona a otro sector.
5. Pulsa **Guardar cambios**.
6. El sistema actualizará el inventario y la jerarquía completa.
7. Si no deseas guardar, pulsa **Cancelar edición**.

En una zona puedes modificar el estado operativo, sensor principal, área y coordenadas. El sistema valida códigos duplicados, relaciones inexistentes y áreas mayores que cero antes de guardar.
1. Selecciona la pestaña del nivel deseado.
2. Busca el registro en **Registros existentes**.
3. Pulsa **Desactivar** o **Activar**.
4. Los elementos inactivos desaparecen de la jerarquía visual, pero permanecen en el inventario para poder reactivarlos.

## Relación y sensores

La parte inferior de la pantalla muestra:

- Centro universitario.
- Fincas pertenecientes al centro.
- Bloques pertenecientes a la finca y su tipo de suelo.
- Sectores pertenecientes al bloque, área y pendiente.
- Zonas pertenecientes al sector, estado, área y sensor principal.

## Validación realizada

Se ejecutó una prueba autenticada que creó temporalmente un centro, finca, bloque, sector y zona con sensor. La jerarquía completa apareció en la consulta de la API y los registros temporales fueron eliminados después de la prueba.

- ESLint superado.
- Compilación de producción de Vite superada.
- Activación y reactivación verificadas.
- Frontend y backend activos.
