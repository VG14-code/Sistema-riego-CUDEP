# Pruebas del modulo 1 (Inicio) contra la API en marcha.
# Uso: python qa/sesion.py && python qa/modulo01_inicio.py
from lib import *
import subprocess, urllib.request, uuid

BASE_DATOS = os.environ.get("QA_DB", "SistemaRiego_QA2")

def sql(consulta):
    salida = subprocess.run(["sqlcmd", "-S", r"(localdb)\MSSQLLocalDB", "-d", BASE_DATOS, "-h", "-1", "-W", "-Q", "SET NOCOUNT ON; " + consulta], capture_output=True, text=True)
    return [linea.strip() for linea in salida.stdout.strip().splitlines() if linea.strip()]

def sin_sesion(ruta, cabeceras=None):
    peticion = urllib.request.Request(B + ruta, headers=cabeceras or {})
    try:
        with urllib.request.urlopen(peticion) as respuesta: return respuesta.status
    except urllib.error.HTTPError as error: return error.code

print("=== 1.1 Resumen operativo: los indicadores coinciden con la base ===")
s, panel = req("GET", "/system/dashboard")
check("El resumen responde con la sesion iniciada", s == 200, (s, panel))
esperados = ["activeSensors", "activeZones", "activeDevices", "zonesIrrigating", "tankLevelPercent", "pumpStatus", "batteryPercent", "todayConsumptionLiters", "activeAlerts", "invalidReadings", "recentActivity", "latestReadings"]
faltan = [campo for campo in esperados if campo not in panel]
check("Trae sensores, zonas, tanque, bomba, bateria, consumo, alertas y actividad", not faltan, "faltan: " + str(faltan))
check("Sensores activos coincide con la base", panel.get("activeSensors") == int(sql("SELECT COUNT(*) FROM IoTSensors WHERE IsActive=1")[0]), panel.get("activeSensors"))
check("Zonas activas coincide con la base", panel.get("activeZones") == int(sql("SELECT COUNT(*) FROM IrrigationZones WHERE IsActive=1")[0]), panel.get("activeZones"))
check("Alertas activas coincide con la base", panel.get("activeAlerts") == int(sql("SELECT COUNT(*) FROM SystemAlerts WHERE Status IN ('Activa','Reconocida')")[0]), panel.get("activeAlerts"))
check("Zonas en riego coincide con la base", panel.get("zonesIrrigating") == int(sql("SELECT COUNT(DISTINCT IrrigationZoneId) FROM IrrigationRuns WHERE Status='En curso'")[0]), panel.get("zonesIrrigating"))
nivel = sql("SELECT TOP 1 CAST(ROUND(CurrentLevelLiters*100.0/CapacityLiters,1) AS decimal(6,1)) FROM WaterTanks WHERE Status<>'Inactivo' ORDER BY Name")
check("El nivel del tanque coincide con la base", nivel and abs(float(panel.get("tankLevelPercent") or -1) - float(nivel[0])) < 0.2, (panel.get("tankLevelPercent"), nivel))
bomba = sql("SELECT TOP 1 CASE WHEN IsRunning=1 THEN 'Encendida' ELSE 'Detenida' END FROM WaterPumps WHERE Status<>'Inactiva' ORDER BY Name")
check("El estado de la bomba coincide con la base", bomba and panel.get("pumpStatus") == bomba[0], (panel.get("pumpStatus"), bomba))
check("Ningun contador llega nulo a la pantalla", all(panel.get(c) is not None for c in ["activeSensors", "activeZones", "activeDevices", "zonesIrrigating", "activeAlerts", "invalidReadings"]), panel)

print("=== 1.1 Tendencia de consumo (grafica del resumen) ===")
s, tendencia = req("GET", "/operations/summary")
check("La grafica recibe la serie diaria de consumo", s == 200 and len(tendencia.get("daily", [])) > 0, (s, str(tendencia)[:120]))
check("La serie trae litros por dia", all("volumeLiters" in dia for dia in tendencia.get("daily", [])), str(tendencia.get("daily", [])[:1]))

print("=== 1.2 Mapa de la granja ===")
s, arbol = req("GET", "/territory/hierarchy")
check("La jerarquia responde", s == 200, s)
conteo = {"centros": len(arbol), "fincas": 0, "bloques": 0, "sectores": 0, "zonas": 0}
zonas = []
for centro in arbol:
    conteo["fincas"] += len(centro.get("farms", []))
    for finca in centro.get("farms", []):
        conteo["bloques"] += len(finca.get("blocks", []))
        for bloque in finca.get("blocks", []):
            conteo["sectores"] += len(bloque.get("sectors", []))
            for sector in bloque.get("sectors", []):
                conteo["zonas"] += len(sector.get("zones", []))
                zonas += sector.get("zones", [])
check("Devuelve los cinco niveles centro>finca>bloque>sector>zona", all(v > 0 for v in conteo.values()), conteo)
check("La cantidad de zonas coincide con la base", conteo["zonas"] == int(sql("SELECT COUNT(*) FROM IrrigationZones WHERE IsActive=1")[0]), conteo)
check("Cada zona trae su estado para el semaforo y el filtro", zonas and all(z.get("status") for z in zonas), zonas[:1])
check("Trae coordenadas para ubicar los puntos", any(z.get("latitude") is not None for z in zonas) or any(f.get("latitude") is not None for c in arbol for f in c.get("farms", [])), "sin coordenadas")

print("=== 1.4 Actividad reciente ===")
actividad = panel.get("recentActivity", [])
check("Incluye la lista de actividad", len(actividad) > 0, len(actividad))
fechas = [x.get("occurredAtUtc") for x in actividad]
check("Viene ordenada de lo mas reciente a lo mas antiguo", fechas == sorted(fechas, reverse=True), fechas[:4])
familias = {str(x.get("type", "")).split(":")[0] for x in actividad}
check("Mezcla auditoria, telemetria, riego o comandos (origen del filtro)", len(familias) >= 2, familias)
check("Cada evento trae tipo, detalle y fecha para el enlace al detalle", all(x.get("type") and x.get("detail") is not None and x.get("occurredAtUtc") for x in actividad), actividad[:1])

print("=== Seguridad del modulo ===")
check("Sin cabecera de autorizacion responde 401", sin_sesion("/system/dashboard") == 401, sin_sesion("/system/dashboard"))
check("Con un token invalido responde 401", req("GET", "/system/dashboard", token="no-es-un-token")[0] == 401, "")
partes = st["token"].split(".")
check("Con la firma alterada responde 401", req("GET", "/system/dashboard", token=partes[0] + "." + partes[1] + ".firmaFalsa")[0] == 401, "")
check("Sin el prefijo Bearer responde 401", sin_sesion("/system/dashboard", {"Authorization": st["token"]}) == 401, "")
check("El mapa tambien exige sesion valida", req("GET", "/territory/hierarchy", token="no-es-un-token")[0] == 401, "")
check("La tendencia tambien exige sesion valida", sin_sesion("/operations/summary") == 401, "")

print("=== Robustez ante parametros invalidos ===")
check("Un identificador mal formado responde 400 y no 500", req("GET", "/telemetry/history?sensorId=esto-no-es-un-guid")[0] == 400, "")
check("Un tamano negativo no rompe el historial", req("GET", "/telemetry/history?take=-5")[0] == 200, "")
s, grande = req("GET", "/telemetry/history?take=999999")
check("Un tamano enorme se acota", s == 200 and len(grande if isinstance(grande, list) else []) <= 1000, len(grande) if isinstance(grande, list) else grande)
check("La auditoria tolera pagina y tamano cero", req("GET", "/audit?page=0&pageSize=0")[0] == 200, "")
check("Un parametro desconocido no altera el resumen", req("GET", "/system/dashboard?parametroInventado=1")[0] == 200, "")

print("=== El resumen refleja los cambios reales ===")
s, tanques = req("GET", "/water-supply/tanks"); tanque = tanques[0]; nivel_original = float(tanque["currentLevelLiters"])
req("POST", f"/water-supply/tanks/{tanque['id']}/level", {"levelLiters": float(tanque["capacityLiters"]) * 0.5, "detail": "QA modulo 1"})
s, panel2 = req("GET", "/system/dashboard")
check("Al cambiar el nivel del tanque el resumen lo muestra", abs(float(panel2.get("tankLevelPercent") or 0) - 50.0) < 0.6, panel2.get("tankLevelPercent"))
sufijo = uuid.uuid4().hex[:5].upper()
s, sensores = req("GET", "/iot/sensors"); sensor = next(x for x in sensores if x["isActive"])
s, lectura = req("POST", "/telemetry/readings", {"sensorId": sensor["id"], "value": 44, "batteryPercent": 88, "messageId": "M1-" + sufijo})
check("Una lectura nueva se acepta", s == 200, (s, str(lectura)[:120]))
check("Una lectura repetida se rechaza como duplicada", req("POST", "/telemetry/readings", {"sensorId": sensor["id"], "value": 44, "batteryPercent": 88, "messageId": "M1-" + sufijo})[0] == 409, "")
s, fuera = req("POST", "/telemetry/readings", {"sensorId": sensor["id"], "value": 9999, "batteryPercent": 50, "messageId": "M1-FR-" + sufijo})
check("Una lectura fuera de rango se marca invalida", s == 200 and fuera.get("isValid") is False, (s, str(fuera)[:120]))
check("Una lectura de un sensor desconocido se rechaza", req("POST", "/telemetry/readings", {"sensorId": str(uuid.uuid4()), "value": 10, "messageId": "M1-XX-" + sufijo})[0] == 400, "")
s, panel3 = req("GET", "/system/dashboard")
check("La lectura nueva aparece en el monitoreo del inicio", any(x.get("sensorId") == sensor["id"] and float(x.get("value", 0)) == 44 for x in panel3.get("latestReadings", [])), "")
check("El indicador de lecturas a revisar cuenta la invalida", int(panel3.get("invalidReadings", 0)) >= 1, panel3.get("invalidReadings"))
req("POST", f"/water-supply/tanks/{tanque['id']}/level", {"levelLiters": nivel_original, "detail": "QA restaurar"})

fallos = [nombre for nombre, ok in results if not ok]
print(f"\nResultado: {len(results) - len(fallos)} de {len(results)} comprobaciones superadas")
if fallos: print("Fallos:", fallos)
json.dump(results, open(os.path.join(os.path.dirname(__file__), "resultado_modulo01.json"), "w"))
