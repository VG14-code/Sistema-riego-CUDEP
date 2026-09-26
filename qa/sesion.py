# Inicia sesion contra la API de pruebas y guarda el token para las baterias de cada modulo.
# La cuenta y la clave se toman de las variables QA_EMAIL, QA_PASSWORD y, si la cuenta
# tiene segundo factor, QA_TOTP_SECRET (la clave compartida que muestra Seguridad 2FA).
from lib import *

correo = os.environ.get("QA_EMAIL", "admin@sistemariego.local")
clave = os.environ.get("QA_PASSWORD")
secreto = os.environ.get("QA_TOTP_SECRET", st.get("secret"))
if not clave: raise SystemExit("Define QA_PASSWORD con la clave de la cuenta de pruebas.")

estado, respuesta = req("POST", "/auth/login", {"email": correo, "password": clave}, token="sin-sesion")
if estado != 200: raise SystemExit(f"No fue posible iniciar sesion ({estado}): {respuesta}")

if "accessToken" in respuesta:
    st["token"] = respuesta["accessToken"]
elif respuesta.get("requiresTwoFactor"):
    if not secreto: raise SystemExit("La cuenta pide segundo factor: define QA_TOTP_SECRET.")
    st["secret"] = secreto
    estado, sesion = req("POST", "/auth/login/2fa", {"email": correo, "challengeToken": respuesta["challengeToken"], "code": totp(secreto)}, token="sin-sesion")
    if estado != 200: raise SystemExit(f"El segundo factor fue rechazado ({estado}): {sesion}")
    st["token"] = sesion["accessToken"]
else:
    raise SystemExit(f"Respuesta de inicio de sesion inesperada: {respuesta}")

save()
print("Sesion lista para las pruebas de modulo.")
