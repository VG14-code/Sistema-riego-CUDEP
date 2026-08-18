# Sprint 4 · Agua, energía y consumo

## Contratos MQTT

| Flujo | Tópico | Datos principales |
|---|---|---|
| Estación hidráulica | `granja/estacion/telemetria` | `pumpCode`, nivel, presión, corriente, estado y `messageId` |
| Orden de bomba | `granja/estacion/bomba/{deviceId}/comando` | `commandId`, `commandType`, `pumpId` |
| ACK de bomba | `granja/estacion/bomba/{deviceId}/ack` | `commandId`, estado e `isRunning` |
| Energía | `granja/energia/telemetria` | generación, batería, consumo, voltaje y `messageId` |
| Caudal | `granja/{zona}/caudal/lectura` | zona, caudal instantáneo y `messageId` |

Las lecturas son idempotentes por `messageId`. Los arranques y paradas de bomba solo cambian a estado confirmado al recibir el ACK correlacionado.

## Protecciones M11

- El nivel igual o inferior al mínimo seguro bloquea el arranque y solicita una parada automática si la bomba estaba activa.
- La corriente superior a `MaximumCurrentAmps` dispara falla por sobrecorriente y una orden de parada.
- Una falla queda bloqueante hasta su reconocimiento técnico.
- El paro global queda persistido en `SystemSafetyState` y bloquea nuevos arranques.
- La capacidad simultánea de válvulas es el mínimo entre el parámetro configurado, la capacidad por caudal nominal y la capacidad por presión disponible.

## Energía M12

El modelo persiste arreglo fotovoltaico, batería, controlador y serie de lecturas. El backend genera alarmas de batería baja y de ausencia de generación en horario diurno. Las reglas con `RequiresSufficientEnergy` no arrancan si la última carga está debajo de `MIN_AUTOMATION_BATTERY_PERCENT`.

## Consumo M13

Al cerrar un riego se promedian las lecturas de caudal del intervalo. Si existen, el registro se marca `Caudal real simulado`; si no, usa el caudal planificado y se marca `Estimado`.

La desviación agronómica es `(volumen real - volumen recomendado) / volumen recomendado × 100`. El costo demostrativo usa `litros / 1000 × WATER_TARIFF_PER_M3`; la tarifa inicial es Q 3.50/m³ y debe sustituirse por la tarifa institucional cuando esté disponible.
