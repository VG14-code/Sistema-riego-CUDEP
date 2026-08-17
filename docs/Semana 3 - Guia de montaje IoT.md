# Semana 3 - Guía de montaje del Raspberry Pi y sensores

## Objetivo

Ensamblar el nodo central descrito en la tesis con un Raspberry Pi 4 Model B y entre dos y tres sondas RK520-02, dejando cada componente identificado de la misma forma en el módulo **Red IoT** del sistema.

## Componentes previstos

- Raspberry Pi 4 Model B con microSD y sistema operativo actualizado.
- Dos o tres sensores RK520-02 para humedad, temperatura y conductividad del suelo.
- Interfaz de adquisición compatible con la variante exacta del sensor (por ejemplo, convertidor RS485 aislado o módulo ADC).
- Fuente regulada para el Raspberry Pi y fuente independiente para sensores cuando el fabricante lo requiera.
- Caja con protección para campo, prensaestopas, borneras, fusible y puesta a tierra apropiada.
- Cableado apantallado, etiquetas y elementos de fijación.

> Importante: no conectar la salida del RK520-02 directamente a un GPIO. Antes del montaje se debe confirmar en la etiqueta y ficha técnica de la unidad si su salida es RS485, 0-5 V, 0-10 V o 4-20 mA. Los GPIO del Raspberry Pi no son entradas analógicas y requieren una interfaz compatible.

## Correspondencia con el sistema

| Elemento físico | Registro recomendado | Código inicial |
|---|---|---|
| Raspberry Pi 4 | Nodo IoT | `RPI-CUDEP-01` |
| Controlador central | Dispositivo | `CTRL-RPI-01` |
| Caja de campo A | Dispositivo | `NODO-CAMPO-A` |
| Caja de campo B | Dispositivo | `NODO-CAMPO-B` |
| Canal de humedad A1 | Sensor | `HUM-SUELO-A1` |
| Canal de humedad B1 | Sensor | `HUM-SUELO-B1` |
| Canal de temperatura A1 | Sensor | `TEMP-SUELO-A1` |

## Secuencia segura de ensamblaje

1. Trabajar con todas las fuentes desconectadas y verificar la tensión de cada componente.
2. Preparar el Raspberry Pi, habilitar la interfaz de comunicación necesaria y asignar una dirección IP reservada.
3. Instalar el convertidor aislado correspondiente entre el Raspberry Pi y el bus de sensores.
4. Alimentar las sondas desde la fuente indicada por el fabricante; compartir referencia eléctrica únicamente cuando el esquema de la interfaz lo requiera.
5. En RS485, cablear `A` con `A` y `B` con `B`, usar topología de bus y colocar terminación solo en sus extremos.
6. Asignar una dirección diferente a cada sonda y documentarla en el campo **Canal** del sistema.
7. Colocar las sondas en dos o tres puntos representativos de las parcelas, evitando bolsas de aire y zonas con acumulación artificial de agua.
8. Proteger empalmes y electrónica dentro de cajas; dejar únicamente la parte sensora preparada para contacto con el suelo según su clasificación.
9. Encender primero la electrónica de adquisición y después comprobar la comunicación desde el Raspberry Pi.
10. Registrar nodos, dispositivos y sensores en **Red IoT**, asignarlos y confirmar que estén en estado **Activo**.

## Prueba funcional de recepción

- Confirmar que cada dirección o canal responde de forma individual.
- Tomar al menos cinco lecturas consecutivas sin errores de comunicación.
- Verificar que humedad y temperatura estén dentro del rango configurado.
- Desconectar temporalmente una sonda y comprobar que la falla pueda identificarse.
- Reiniciar el Raspberry Pi y confirmar que el servicio de adquisición se recupera automáticamente.
- Registrar fecha, responsable, ubicación, número de serie y resultado.

## Calibración inicial

1. Preparar una condición de referencia controlada.
2. Seleccionar el sensor en **Red IoT > Calibración**.
3. Introducir el valor de referencia y el valor medido.
4. Aplicar la calibración; el sistema calculará la corrección como `referencia - medición`.
5. Repetir la lectura y documentar las condiciones de la prueba.

## Criterios para considerar cumplido el montaje

- Raspberry Pi registrado, protegido y accesible en la red local.
- Dos o tres sondas identificadas con código y número de serie.
- Cableado, alimentación e interfaz revisados antes de energizar.
- Comunicación estable durante cinco lecturas consecutivas.
- Sensores asignados a sus dispositivos y ubicación correspondiente.
- Calibración inicial guardada en el sistema.
- Evidencia fotográfica del montaje físico y capturas de la pantalla Red IoT.

La manipulación de bombas, válvulas, tensión de red o tableros eléctricos debe ser realizada por personal calificado. Esta semana solo requiere el nodo de procesamiento y los sensores; los actuadores deben permanecer aislados hasta su etapa de integración.
