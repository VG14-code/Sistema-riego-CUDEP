# Módulo 3 · Seguridad

Cierre del módulo el 26 de septiembre de 2026. La referencia es la hoja «Módulos detallados» de `Planificacion final Victor Gabriel Madrid .xlsx`.

## Qué pedía la planificación y dónde quedó

| Submódulo | Dónde está en el sistema | Estado |
|---|---|---|
| Usuarios | Seguridad → Usuarios, roles y sesiones: alta, edición, código de personal, centro, finca, estado y restablecimiento de contraseña | Completo |
| Roles | Misma pantalla, sección «Administración de roles»: crear, editar, activar, desactivar y eliminar | Completo |
| Permisos | Catálogo de permisos con búsqueda y paginación | Completo |
| Roles y permisos | Matriz rol × permiso, con casillas por permiso y confirmación TOTP | Completo |
| Autenticación | Inicio de sesión con JWT, renovación por cookie HttpOnly, bloqueo por intentos y recuperación de contraseña | Completo |
| Doble autenticación | Seguridad → Doble autenticación: QR, clave manual, verificación y ocho códigos de recuperación | Completo |
| Sesiones activas | Sección «Sesiones activas»: usuario, IP, inicio, vencimiento, búsqueda, exportación y revocación | Completo |
| Autorizaciones críticas | Diálogo de confirmación con código TOTP en cada operación sensible, y registro en la bitácora | Completo |

## Defectos encontrados y corregidos

1. **Revocar una sesión no cerraba el acceso.** El token no identificaba su sesión: al revocarla desde el panel solo se cortaba la renovación, y el dispositivo seguía operando hasta quince minutos más. Ahora el token lleva el identificador de su sesión y la validación lo rechaza en cuanto la sesión queda revocada, se bloquea la cuenta o se cierra sesión.
2. **No se podía bloquear a un usuario desde la pantalla.** El selector envía el nombre del estado («Blocked») y la API solo aceptaba el número, así que respondía «error de validación». Ahora acepta el nombre, sin distinguir mayúsculas, y rechaza los estados que no existen indicando cuáles son válidos.
3. **Una contraseña débil se reportaba como conflicto.** El alta de usuarios respondía 409, el mismo código que usa un correo repetido. Ahora responde 400, que es lo que corresponde a un dato inválido.

## Pruebas ejecutadas

**Funcionales y de validación:** `python qa/sesion.py && python qa/modulo03_seguridad.py` → **53 de 53 comprobaciones superadas**.

| Grupo | Qué comprueba |
|---|---|
| Usuarios | Alta con y sin TOTP, correo repetido, contraseña débil, adscripción a código de personal, centro y finca |
| Autenticación | Inicio de sesión correcto, contraseña incorrecta y correo inexistente con la misma respuesta neutra, correo mal formado |
| Sesiones | La sesión aparece con IP y vencimiento y sin exponer el token; al revocarla, su token deja de servir |
| Roles y permisos | Alta, nombre repetido, catálogo de permisos, matriz rol × permiso, permisos desconocidos y cambios sin TOTP |
| Herencia y excepciones | El usuario hereda los permisos de su rol y una denegación individual gana sobre el rol |
| Protecciones | No se elimina un rol con usuarios, no se asigna un rol inactivo, y el rol Administrador no se desactiva ni se elimina |
| Contraseñas | Restablecimiento con contraseña temporal, cambio obligatorio antes de operar y bloqueo de la cuenta |
| Doble autenticación | Estado, rechazo de códigos inválidos, desafío en el inicio de sesión y segundo factor equivocado |
| Autorizaciones críticas | Sin código y con código equivocado se rechaza; la bitácora registra roles, usuarios y responsable |

**En el navegador:** cambio de estado de un usuario con el diálogo TOTP real (`PATCH /users/{id}/status` → 204, antes 400), pantalla de alta del segundo factor con QR y clave manual, y listados con búsqueda, paginación y exportación.

**Pruebas automáticas del repositorio:** 219 de 219, incluidas cinco nuevas para estas correcciones (token ligado a su sesión, enlace de la sesión renovada, contraseña débil, bloqueo por nombre de estado y estado inexistente).

## Capturas

| Vista | Archivo |
|---|---|
| Usuarios, roles, sesiones y matriz | [modulo-03-usuarios-vista.png](img/modulo-03-usuarios-vista.png) · [completo](img/modulo-03-usuarios-completo.png) · [teléfono](img/modulo-03-usuarios-movil.png) |
| Alta del segundo factor con QR | [modulo-03-2fa-vista.png](img/modulo-03-2fa-vista.png) · [completo](img/modulo-03-2fa-completo.png) |

La captura del segundo factor se toma con `node scripts/2fa-evidence.mjs` desde `frontend`, con una cuenta que aún no tenga 2FA activo. La clave compartida sale difuminada y la clave del QR que aparece en la imagen se rotó al volver a activar el segundo factor.

## Pendiente

Nada del módulo 3. Los códigos de recuperación se entregan al activar el segundo factor y sirven para entrar cuando no se tiene el teléfono; si se pierden, hay que desactivar y volver a activar el 2FA para obtener un juego nuevo.
