# Pruebas del modulo 2 (Datos maestros) contra la API en marcha.
# Uso: python qa/sesion.py && python qa/modulo02_datos_maestros.py
from lib import *
import uuid, urllib.request

U = uuid.uuid4().hex[:5].upper()
creados = {"centers": [], "farms": [], "blocks": [], "sectors": [], "zones": [], "catalogs": [], "brands": [], "models": [], "soils": [], "cropTypes": [], "crops": [], "stages": []}

def sin_sesion(ruta):
    try:
        with urllib.request.urlopen(urllib.request.Request(B + ruta)) as r: return r.status
    except urllib.error.HTTPError as e: return e.code

print("=== 2.1 a 2.5 Jerarquia territorial: centro > finca > bloque > sector > zona ===")
s, centro = req("POST", "/territory/centers", {"code": "C" + U, "name": "Centro QA " + U, "location": "Petén", "contact": "qa@cudep.local"})
check("Crear centro universitario con ubicacion y contacto", s in (200, 201) and centro.get("id"), (s, centro))
creados["centers"].append(centro.get("id"))
s, r = req("POST", "/territory/centers", {"code": "C" + U, "name": "Centro repetido"})
check("Rechaza un centro con codigo repetido", s == 409, (s, str(r)[:100]))

s, finca = req("POST", "/territory/farms", {"universityCenterId": centro["id"], "code": "F" + U, "name": "Finca QA " + U, "location": "San Benito", "latitude": 16.92, "longitude": -89.88})
check("Crear finca ligada al centro", s in (200, 201), (s, finca))
creados["farms"].append(finca.get("id"))
s, r = req("POST", "/territory/farms", {"universityCenterId": str(uuid.uuid4()), "code": "FX" + U, "name": "Finca huerfana"})
check("Rechaza una finca con un centro inexistente", s == 400, (s, str(r)[:100]))

s, suelos = req("GET", "/agronomy/soil-types"); suelo = suelos[0]
s, bloque = req("POST", "/territory/blocks", {"farmId": finca["id"], "soilTypeId": suelo["id"], "code": "B" + U, "name": "Bloque QA " + U, "areaHectares": 2.5, "description": "Parcela de prueba"})
check("Crear bloque con finca, suelo y area", s in (200, 201), (s, bloque))
creados["blocks"].append(bloque.get("id"))
s, r = req("POST", "/territory/blocks", {"farmId": finca["id"], "code": "B0" + U, "name": "Bloque sin area", "areaHectares": 0})
check("Rechaza un bloque con area cero", s == 400, (s, str(r)[:100]))

s, sector = req("POST", "/territory/sectors", {"farmBlockId": bloque["id"], "code": "S" + U, "name": "Sector QA " + U, "areaHectares": 1.2, "slopePercent": 3})
check("Crear sector con area y pendiente", s in (200, 201), (s, sector))
creados["sectors"].append(sector.get("id"))
s, r = req("POST", "/territory/sectors", {"farmBlockId": bloque["id"], "code": "S" + U, "name": "Sector repetido", "areaHectares": 1, "slopePercent": 1})
check("Rechaza un sector con codigo repetido", s == 409, (s, str(r)[:100]))

s, estados = req("GET", "/catalogs/OperationalStatus"); activo = next(x for x in estados if x["code"] == "ACTIVE")
s, zona = req("POST", "/territory/zones", {"irrigationSectorId": sector["id"], "code": "Z" + U, "name": "Zona QA " + U, "areaHectares": 0.4, "operationalStatusId": activo["id"], "primarySensorId": None, "valveDeviceId": None, "latitude": 16.92, "longitude": -89.88})
check("Crear zona de riego ligada al sector", s in (200, 201), (s, zona))
creados["zones"].append(zona.get("id"))

s, arbol = req("GET", "/territory/hierarchy")
nuevo = next((c for c in arbol if c["id"] == centro["id"]), None)
ruta_completa = bool(nuevo and nuevo["farms"] and nuevo["farms"][0]["blocks"] and nuevo["farms"][0]["blocks"][0]["sectors"] and nuevo["farms"][0]["blocks"][0]["sectors"][0]["zones"])
check("Lo creado aparece completo en la jerarquia", ruta_completa, nuevo)
check("El bloque conserva el tipo de suelo asignado", nuevo and nuevo["farms"][0]["blocks"][0].get("soilType") == suelo["name"], nuevo["farms"][0]["blocks"][0].get("soilType") if nuevo else None)

print("=== Edicion y baja logica ===")
s, r = req("PUT", "/territory/centers/" + centro["id"], {"code": "C" + U, "name": "Centro QA editado " + U, "location": "Petén", "contact": "qa@cudep.local", "isActive": True})
check("Editar un centro", s in (200, 204), (s, str(r)[:100]))
s, centros = req("GET", "/territory/centers")
check("El nombre editado queda guardado", any(x["id"] == centro["id"] and "editado" in x["name"] for x in centros), "")
s, r = req("PATCH", f"/territory/zones/{zona['id']}/toggle", {})
check("Desactivar una zona (baja logica)", s in (200, 204), (s, str(r)[:100]))
s, zonas = req("GET", "/territory/zones")
check("La zona desactivada sigue en el listado para poder reactivarla", any(x["id"] == zona["id"] and x["isActive"] is False for x in zonas), "")
req("PATCH", f"/territory/zones/{zona['id']}/toggle", {})

print("=== 2.6 a 2.9 Catalogos agronomicos ===")
s, nuevo_suelo = req("POST", "/agronomy/soil-types", {"name": "Suelo QA " + U, "fieldCapacityPercent": 22, "saturationPercent": 42, "infiltrationMillimetersHour": 18, "irrigationCorrectionFactor": 1.2})
check("Crear tipo de suelo con sus parametros", s in (200, 201), (s, str(nuevo_suelo)[:120]))
creados["soils"].append(nuevo_suelo.get("id"))
s, r = req("POST", "/agronomy/soil-types", {"name": "Suelo invalido " + U, "fieldCapacityPercent": 50, "saturationPercent": 30, "infiltrationMillimetersHour": 10})
check("Rechaza un suelo con saturacion menor que la capacidad de campo", s == 400, (s, str(r)[:100]))
s, tipo = req("POST", "/agronomy/crop-types", {"name": "Categoria QA " + U})
check("Crear tipo de cultivo (pantalla de categorias)", s in (200, 201), (s, str(tipo)[:120]))
creados["cropTypes"].append(tipo.get("id"))
s, cultivo = req("POST", "/agronomy/crops", {"cropTypeId": tipo["id"], "name": "Cultivo QA " + U, "scientificName": "Testus qa", "description": "Cultivo de prueba"})
check("Crear cultivo ligado a su tipo", s in (200, 201), (s, str(cultivo)[:120]))
creados["crops"].append(cultivo.get("id"))
s, r = req("POST", "/agronomy/crops", {"cropTypeId": str(uuid.uuid4()), "name": "Cultivo huerfano"})
check("Rechaza un cultivo con un tipo inexistente", s == 400, (s, str(r)[:100]))
s, etapa1 = req("POST", "/agronomy/stages", {"cropId": cultivo["id"], "name": "Germinación", "sequence": 1, "estimatedDays": 12})
check("Crear etapa fenologica con su orden", s in (200, 201), (s, str(etapa1)[:120]))
creados["stages"].append(etapa1.get("id"))
s, r = req("POST", "/agronomy/stages", {"cropId": cultivo["id"], "name": "Repetida", "sequence": 1, "estimatedDays": 10})
check("Rechaza dos etapas con la misma secuencia en un cultivo", s == 400, (s, str(r)[:100]))
s, r = req("POST", "/agronomy/stages", {"cropId": cultivo["id"], "name": "Sin dias", "sequence": 2, "estimatedDays": 0})
check("Rechaza una etapa con duracion cero", s == 400, (s, str(r)[:100]))

print("=== 2.10 Unidades de medida y conversiones ===")
s, litro = req("GET", "/catalogs/MeasurementUnit"); base_litro = next(x for x in litro if x["code"] == "LITER")
s, ml = req("POST", "/catalogs/MeasurementUnit", {"code": "ML" + U, "name": "Mililitro " + U, "symbol": "mL", "baseUnitCode": "LITER", "conversionFactorToBase": 0.001})
check("Crear unidad con unidad base y factor", s in (200, 201) and ml.get("baseUnitCode") == "LITER", (s, str(ml)[:120]))
creados["catalogs"].append(("MeasurementUnit", ml.get("id")))
s, conv = req("POST", "/catalogs/measurement-units/convert", {"fromUnitId": ml["id"], "toUnitId": base_litro["id"], "value": 2500})
check("Convertir 2500 mL a 2.5 L", s == 200 and float(conv["convertedValue"]) == 2.5, (s, conv))
s, r = req("POST", "/catalogs/measurement-units/convert", {"fromUnitId": ml["id"], "toUnitId": next(x for x in litro if x["code"] == "CELSIUS")["id"], "value": 1})
check("Rechaza convertir entre magnitudes distintas", s == 400, (s, str(r)[:100]))
s, r = req("POST", "/catalogs/MeasurementUnit", {"code": "BAD" + U, "name": "Factor invalido", "symbol": "x", "conversionFactorToBase": 0})
check("Rechaza un factor de conversion cero", s == 400, (s, str(r)[:100]))

print("=== 2.11 a 2.20 Catalogos tecnicos y operativos ===")
tipos = ["SensorType", "DeviceType", "OperationalStatus", "ValveType", "PumpType", "WaterSource", "AlertType", "SuspensionReason", "ReadingFrequency"]
for tipo_catalogo in tipos:
    s, lista = req("GET", "/catalogs/" + tipo_catalogo)
    check(f"El catalogo {tipo_catalogo} responde y tiene registros", s == 200 and len(lista) > 0, (s, len(lista) if isinstance(lista, list) else lista))
cuerpo = {"code": "QA" + U, "name": "Tipo de sensor QA " + U, "description": "Alta de prueba", "symbol": None}
s, creado = req("POST", "/catalogs/SensorType", cuerpo)
check("Crear un elemento de catalogo", s in (200, 201), (s, str(creado)[:120]))
creados["catalogs"].append(("SensorType", creado.get("id")))
s, r = req("POST", "/catalogs/SensorType", cuerpo)
check("Rechaza un codigo repetido dentro del mismo catalogo", s == 409, (s, str(r)[:100]))
s, r = req("PUT", f"/catalogs/SensorType/{creado['id']}", {**cuerpo, "name": "Tipo editado " + U})
check("Editar un elemento de catalogo", s == 200 and r.get("name") == "Tipo editado " + U, (s, str(r)[:120]))
s, r = req("POST", "/catalogs/CatalogoInventado", cuerpo)
check("Rechaza un tipo de catalogo inexistente", s == 400, (s, str(r)[:100]))
s, frecuencia = req("POST", "/catalogs/ReadingFrequency", {"code": "FQ" + U, "name": "Cada dos minutos " + U, "symbol": "120 s", "intervalSeconds": 120})
check("Crear frecuencia de lectura con su intervalo en segundos", s in (200, 201) and frecuencia.get("intervalSeconds") == 120, (s, str(frecuencia)[:120]))
creados["catalogs"].append(("ReadingFrequency", frecuencia.get("id")))
s, r = req("POST", "/catalogs/ReadingFrequency", {"code": "FQX" + U, "name": "Sin intervalo", "symbol": "x"})
check("Rechaza una frecuencia sin intervalo", s == 400, (s, str(r)[:100]))

print("=== 2.13 Marcas y modelos con especificaciones ===")
s, tipos_dispositivo = req("GET", "/catalogs/DeviceType"); tipo_dispositivo = tipos_dispositivo[0]
s, marca = req("POST", "/device-catalogs/brands", {"code": "MA" + U, "name": "Marca QA " + U, "description": "Fabricante de prueba"})
check("Crear marca", s in (200, 201), (s, str(marca)[:120]))
creados["brands"].append(marca.get("id"))
s, modelo = req("POST", "/device-catalogs/models", {"deviceBrandId": marca["id"], "deviceTypeId": tipo_dispositivo["id"], "code": "MO" + U, "name": "Modelo QA " + U, "precision": "±2 %", "voltage": "3.3 V", "communicationProtocol": "MQTT/WiFi"})
check("Crear modelo con precision, voltaje y protocolo", s in (200, 201), (s, str(modelo)[:150]))
creados["models"].append(modelo.get("id"))
s, modelos = req("GET", "/device-catalogs/models"); guardado = next((x for x in modelos if x["id"] == modelo["id"]), {})
check("El listado devuelve las especificaciones tecnicas", guardado.get("precision") == "±2 %" and guardado.get("voltage") == "3.3 V" and guardado.get("communicationProtocol") == "MQTT/WiFi", guardado)
s, r = req("POST", "/device-catalogs/models", {"deviceBrandId": str(uuid.uuid4()), "deviceTypeId": tipo_dispositivo["id"], "code": "MX" + U, "name": "Modelo huerfano"})
check("Rechaza un modelo con una marca inexistente", s in (400, 404), (s, str(r)[:100]))

print("=== Integridad referencial ===")
s, r = req("DELETE", f"/agronomy/crop-types/{tipo['id']}")
check("No deja borrar un tipo de cultivo que tiene cultivos", s in (400, 409), (s, str(r)[:120]))
s, r = req("DELETE", f"/device-catalogs/brands/{marca['id']}")
check("No deja borrar una marca con modelos registrados", s in (400, 409), (s, str(r)[:120]))

print("=== Seguridad y exportacion de las listas ===")
check("La jerarquia exige sesion valida", sin_sesion("/territory/hierarchy") == 401, "")
check("Los catalogos exigen sesion valida", sin_sesion("/catalogs/SensorType") == 401, "")
def exportar(formato):
    cuerpo = json.dumps({"title": "Fincas", "headers": ["Código", "Nombre"], "rows": [["F1", "Finca de prueba"]]}).encode()
    peticion = urllib.request.Request(B + "/exports/" + formato, data=cuerpo, method="POST",
                                      headers={"Content-Type": "application/json", "Authorization": "Bearer " + st["token"]})
    with urllib.request.urlopen(peticion) as respuesta: return respuesta.status, respuesta.read()
estado, excel = exportar("xlsx")
check("La lista se exporta a Excel", estado == 200 and excel[:2] == b"PK" and len(excel) > 2000, (estado, len(excel)))
estado, pdf = exportar("pdf")
check("La lista se exporta a PDF", estado == 200 and pdf[:4] == b"%PDF" and len(pdf) > 800, (estado, len(pdf)))

print("=== Limpieza de los datos de prueba ===")
for identificador in creados["models"]: req("DELETE", "/device-catalogs/models/" + identificador)
for identificador in creados["brands"]: req("DELETE", "/device-catalogs/brands/" + identificador)
for identificador in creados["stages"]: req("DELETE", "/agronomy/stages/" + identificador)
for identificador in creados["crops"]: req("DELETE", "/agronomy/crops/" + identificador)
for identificador in creados["cropTypes"]: req("DELETE", "/agronomy/crop-types/" + identificador)
for identificador in creados["soils"]: req("DELETE", "/agronomy/soil-types/" + identificador)
for tipo_catalogo, identificador in creados["catalogs"]:
    if identificador: req("DELETE", f"/catalogs/{tipo_catalogo}/{identificador}")
for nivel in ("zones", "sectors", "blocks", "farms", "centers"):
    for identificador in creados[nivel]:
        if identificador: req("PATCH", f"/territory/{nivel}/{identificador}/toggle", {})
print("   registros de prueba desactivados o eliminados")

fallos = [nombre for nombre, ok in results if not ok]
print(f"\nResultado: {len(results) - len(fallos)} de {len(results)} comprobaciones superadas")
if fallos: print("Fallos:", fallos)
json.dump(results, open(os.path.join(os.path.dirname(__file__), "resultado_modulo02.json"), "w"))
