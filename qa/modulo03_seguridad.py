# Pruebas del modulo 3 (Seguridad) contra la API en marcha.
# Uso: python qa/sesion.py && python qa/modulo03_seguridad.py
from lib import *
import uuid, urllib.request, time

U = uuid.uuid4().hex[:5].upper()
CORREO = f"seguridad{U.lower()}@test.local"
CLAVE = "QaTemporal#2026x"
ROL = "Rol QA " + U
creados = {"usuarios": [], "roles": []}

def sin_sesion(ruta):
    try:
        with urllib.request.urlopen(urllib.request.Request(B + ruta)) as r: return r.status
    except urllib.error.HTTPError as e: return e.code

def login(correo, clave):
    # El limitador de intentos responde 429; se espera su ventana y se reintenta.
    for intento in range(6):
        estado, cuerpo = req("POST", "/auth/login", {"email": correo, "password": clave}, token="sin-sesion")
        if estado != 429: return estado, cuerpo
        time.sleep(20)
    return estado, cuerpo

def totp_actual():
    # Un codigo recien emitido evita que expire a mitad de la peticion.
    while 30 - int(time.time()) % 30 < 4: time.sleep(1)
    return totp(st["secret"])

print("=== 3.1 Usuarios: alta, adscripcion y estados ===")
s, r = req("POST", "/auth/register", {"email": CORREO, "password": CLAVE, "fullName": "Usuario Seguridad " + U})
check("Crear un usuario sin TOTP es rechazado", s == 403, (s, str(r)[:100]))
s, r = req("POST", "/auth/register", {"email": CORREO, "password": CLAVE, "fullName": "Usuario Seguridad " + U}, critical=True)
check("Crear un usuario con TOTP", s in (200, 201), (s, str(r)[:120]))
s, usuarios = req("GET", "/users")
usuario = next((x for x in usuarios if x["email"] == CORREO), None)
check("El usuario aparece en el listado", usuario is not None, CORREO)
creados["usuarios"].append(usuario["id"])
s, r = req("POST", "/auth/register", {"email": CORREO, "password": CLAVE, "fullName": "Duplicado"}, critical=True)
check("Rechaza un correo repetido", s in (400, 409), (s, str(r)[:120]))
s, r = req("POST", "/auth/register", {"email": f"debil{U.lower()}@test.local", "password": "12345678", "fullName": "Clave debil"}, critical=True)
check("Una contrasena que no cumple la politica se rechaza como dato invalido (400)", s == 400, (s, str(r)[:120]))

s, centros = req("GET", "/territory/centers"); centro = next(x for x in centros if x["isActive"])
s, fincas = req("GET", "/territory/farms"); finca = next(x for x in fincas if x["isActive"] and x["universityCenterId"] == centro["id"])
s, r = req("PUT", "/users/" + usuario["id"], {"fullName": "Usuario Seguridad " + U, "email": CORREO, "personnelCode": "EMP-" + U, "universityCenterId": centro["id"], "farmId": finca["id"]}, critical=True)
check("Asociar el usuario con codigo de personal, centro y finca", s in (200, 204), (s, str(r)[:120]))
s, usuarios = req("GET", "/users"); usuario = next(x for x in usuarios if x["id"] == creados["usuarios"][0])
check("La adscripcion queda guardada", usuario.get("personnelCode") == "EMP-" + U and usuario.get("farmId") == finca["id"], usuario)
s, r = req("PUT", "/users/" + usuario["id"], {"fullName": "Usuario Seguridad " + U, "email": CORREO, "personnelCode": "EMP-" + U, "universityCenterId": centro["id"], "farmId": finca["id"]})
check("Editar un usuario sin TOTP es rechazado", s == 403, (s, str(r)[:100]))

print("=== 3.5 Autenticacion ===")
s, sesion = login(CORREO, CLAVE)
check("El usuario nuevo inicia sesion", s == 200 and "accessToken" in sesion, (s, str(sesion)[:120]))
token_usuario = sesion.get("accessToken")
check("La respuesta incluye vencimiento del token", bool(sesion.get("accessTokenExpiresAtUtc")), sesion.keys())
s, r = login(CORREO, "ClaveIncorrecta#1")
check("Una contrasena incorrecta responde 401 con mensaje neutro", s == 401 and "Credenciales" in json.dumps(r, ensure_ascii=False), (s, str(r)[:120]))
s, r = login("no-existe@test.local", CLAVE)
check("Un correo inexistente responde igual que una clave incorrecta", s == 401, (s, str(r)[:120]))
s, r = login("no-es-un-correo", CLAVE)
check("Un correo mal formado se rechaza con 400", s == 400, (s, str(r)[:120]))

print("=== 3.7 Sesiones activas ===")
s, sesiones = req("GET", "/sessions")
mias = [x for x in sesiones if x["email"] == CORREO and x["isActive"]]
check("La sesion del usuario aparece en el listado", len(mias) > 0, len(sesiones))
check("El listado expone IP y vencimiento sin mostrar el token", mias and "expiresAtUtc" in mias[0] and not any("token" in k.lower() for k in mias[0]), list(mias[0].keys()) if mias else [])
s, r = req("DELETE", "/sessions/" + mias[0]["id"])
check("Revocar la sesion desde el panel", s in (200, 204), (s, str(r)[:100]))
s, r = req("GET", "/system/dashboard", token=token_usuario)
check("El token de la sesion revocada deja de servir", s == 401, (s, str(r)[:100]))

print("=== 3.2 y 3.3 Roles y permisos ===")
s, rol = req("POST", "/roles", {"name": ROL, "description": "Rol de prueba", "isActive": True}, critical=True)
check("Crear un rol", s == 201, (s, str(rol)[:120]))
creados["roles"].append(rol.get("id"))
s, r = req("POST", "/roles", {"name": ROL.lower(), "description": "Duplicado", "isActive": True}, critical=True)
check("Rechaza un rol con el mismo nombre en otra caja", s == 409, (s, str(r)[:120]))
s, permisos = req("GET", "/roles/permissions")
check("El catalogo de permisos responde", s == 200 and len(permisos) > 10, len(permisos) if isinstance(permisos, list) else permisos)
check("Cada permiso trae codigo y descripcion", all(x.get("code") and x.get("description") for x in permisos), permisos[:1])
elegidos = [permisos[0]["code"], permisos[1]["code"]]
import urllib.parse
s, r = req("PUT", f"/roles/{urllib.parse.quote(ROL)}/permissions", {"permissions": elegidos}, critical=True)
check("Asignar permisos al rol (matriz rol x permiso)", s in (200, 204), (s, str(r)[:120]))
s, roles = req("GET", "/roles"); guardado = next(x for x in roles if x["name"] == ROL)
check("El rol conserva los permisos asignados", set(elegidos) <= set(guardado["permissions"]), guardado.get("permissions"))
s, r = req("PUT", f"/roles/{urllib.parse.quote(ROL)}/permissions", {"permissions": ["permiso.inventado"]}, critical=True)
check("Rechaza permisos desconocidos en la matriz", s == 400, (s, str(r)[:120]))
s, r = req("PUT", f"/roles/{urllib.parse.quote(ROL)}/permissions", {"permissions": elegidos})
check("Cambiar la matriz sin TOTP es rechazado", s == 403, (s, str(r)[:100]))

print("=== 3.4 Roles aplicados al usuario ===")
s, r = req("PUT", f"/users/{usuario['id']}/roles", {"roles": [ROL]}, critical=True)
check("Asignar el rol al usuario", s in (200, 204), (s, str(r)[:120]))
s, efectivos = req("GET", f"/users/{usuario['id']}/permissions")
concedidos = [x["code"] for x in efectivos if x.get("effectiveGranted")]
check("El usuario hereda los permisos de su rol", set(elegidos) <= set(concedidos), concedidos[:6])
s, r = req("PUT", f"/users/{usuario['id']}/permissions", {"overrides": [{"code": elegidos[0], "isGranted": False}]}, critical=True)
check("Se puede denegar un permiso a un usuario concreto", s in (200, 204), (s, str(r)[:120]))
s, efectivos = req("GET", f"/users/{usuario['id']}/permissions")
denegado = next(x for x in efectivos if x["code"] == elegidos[0])
check("La denegacion individual gana sobre el rol", denegado.get("effectiveGranted") is False, denegado)
s, r = req("DELETE", "/roles/" + rol["id"], critical=True)
check("No deja eliminar un rol asignado a usuarios", s == 409, (s, str(r)[:120]))
s, r = req("PATCH", f"/roles/{rol['id']}/status", {"isActive": False}, critical=True)
check("Desactivar el rol", s == 204, (s, str(r)[:120]))
s, r = req("PUT", f"/users/{usuario['id']}/roles", {"roles": [ROL]}, critical=True)
check("Un rol inactivo ya no puede asignarse", s == 400, (s, str(r)[:120]))

print("=== Proteccion del rol Administrador ===")
s, roles = req("GET", "/roles"); admin = next(x for x in roles if x["name"] == "Administrador")
check("No deja desactivar el rol Administrador", req("PATCH", f"/roles/{admin['id']}/status", {"isActive": False}, critical=True)[0] == 400, "")
check("No deja eliminar el rol Administrador", req("DELETE", f"/roles/{admin['id']}", critical=True)[0] == 400, "")

print("=== 3.1 Bloqueo y restablecimiento de contrasena ===")
s, r = req("PUT", f"/users/{usuario['id']}/roles", {"roles": ["Operador"]}, critical=True)
s, temporal = req("POST", f"/users/{usuario['id']}/reset-password", {}, critical=True)
check("Restablecer la contrasena entrega una temporal", s == 200 and temporal.get("temporaryPassword"), (s, str(temporal)[:80]))
s, sesion2 = login(CORREO, temporal["temporaryPassword"])
check("La contrasena temporal permite entrar", s == 200, (s, str(sesion2)[:120]))
check("La cuenta queda obligada a cambiar la contrasena", sesion2.get("user", {}).get("mustChangePassword") is True, sesion2.get("user"))
s, r = req("GET", "/system/dashboard", token=sesion2["accessToken"])
check("Mientras no cambie la contrasena no puede operar el sistema", s == 403, (s, str(r)[:120]))
s, r = req("PATCH", f"/users/{usuario['id']}/status", {"status": "Blocked"}, critical=True)
check("Bloquear la cuenta", s in (200, 204), (s, str(r)[:120]))
s, r = login(CORREO, temporal["temporaryPassword"])
check("Una cuenta bloqueada no puede iniciar sesion", s == 401, (s, str(r)[:120]))
req("PATCH", f"/users/{usuario['id']}/status", {"status": "Active"}, critical=True)

print("=== 3.6 Doble autenticacion ===")
s, estado = req("GET", "/security/2fa/status")
check("El estado de 2FA responde", s == 200 and estado.get("enabled") is True, (s, estado))
s, r = req("POST", "/security/2fa/enable", {"code": "000000"})
check("Un codigo TOTP invalido no activa el segundo factor", s in (400, 401), (s, str(r)[:120]))
s, desafio = login("admin@sistemariego.local", os.environ.get("QA_PASSWORD", "")) if os.environ.get("QA_PASSWORD") else (0, {})
if os.environ.get("QA_PASSWORD"):
    check("Con 2FA activo el login entrega un desafio en vez del token", desafio.get("requiresTwoFactor") is True and "accessToken" not in desafio, str(desafio)[:120])
    for intento in range(6):
        s, r = req("POST", "/auth/login/2fa", {"email": "admin@sistemariego.local", "challengeToken": desafio["challengeToken"], "code": "000000"}, token="sin-sesion")
        if s != 429: break
        time.sleep(20)
    check("Un codigo equivocado no completa el segundo factor", s == 401, (s, str(r)[:120]))

print("=== 3.8 Autorizaciones criticas y bitacora ===")
s, r = req("POST", "/roles", {"name": "Sin TOTP " + U, "description": "x", "isActive": True})
check("Una operacion critica sin codigo TOTP se rechaza", s == 403, (s, str(r)[:120]))
s, r = req("POST", "/roles", {"name": "TOTP malo " + U, "description": "x", "isActive": True}, critical=False)
peticion = {"name": "TOTP malo " + U, "description": "x", "isActive": True}
import urllib.request as _u
solicitud = _u.Request(B + "/roles", data=json.dumps(peticion).encode(), method="POST",
                       headers={"Content-Type": "application/json", "Authorization": "Bearer " + st["token"], "X-TOTP-Code": "000000"})
try:
    with _u.urlopen(solicitud) as resp: codigo = resp.status
except urllib.error.HTTPError as e: codigo = e.code
check("Un codigo TOTP equivocado tampoco autoriza la operacion", codigo == 403, codigo)
s, auditoria = req("GET", "/audit?page=1&pageSize=50")
eventos = json.dumps(auditoria, ensure_ascii=False)
check("La bitacora registra la creacion de roles", "ROLE_CREATED" in eventos, eventos[:150])
check("La bitacora registra los cambios de usuario", "USER_" in eventos, eventos[:150])
check("La auditoria identifica al responsable", '"userEmail"' in eventos, eventos[:150])

print("=== Seguridad de los propios endpoints ===")
check("El listado de usuarios exige sesion", sin_sesion("/users") == 401, "")
check("El listado de roles exige sesion", sin_sesion("/roles") == 401, "")
check("Las sesiones activas exigen sesion", sin_sesion("/sessions") == 401, "")
check("La auditoria exige sesion", sin_sesion("/audit") == 401, "")

print("=== Limpieza ===")
for identificador in creados["usuarios"]:
    req("PATCH", f"/users/{identificador}/status", {"status": "Disabled"}, critical=True)
for identificador in creados["roles"]:
    req("PUT", f"/users/{creados['usuarios'][0]}/roles", {"roles": ["Operador"]}, critical=True)
    req("DELETE", "/roles/" + identificador, critical=True)
print("   usuario de prueba deshabilitado y rol eliminado")

fallos = [nombre for nombre, ok in results if not ok]
print(f"\nResultado: {len(results) - len(fallos)} de {len(results)} comprobaciones superadas")
if fallos: print("Fallos:", fallos)
json.dump(results, open(os.path.join(os.path.dirname(__file__), "resultado_modulo03.json"), "w"))
