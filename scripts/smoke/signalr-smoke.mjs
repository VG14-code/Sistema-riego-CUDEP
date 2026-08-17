import signalR from '../../frontend/node_modules/@microsoft/signalr/dist/cjs/index.js'
const { HubConnectionBuilder, LogLevel } = signalR

const apiUrl = process.env.SMOKE_API_URL ?? 'http://localhost:5080'
const response = await fetch(`${apiUrl}/api/auth/login`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email: process.env.SMOKE_EMAIL, password: process.env.SMOKE_PASSWORD }),
})
if (!response.ok) throw new Error(`Login falló: ${response.status}`)
const session = await response.json()

const connection = new HubConnectionBuilder()
  .withUrl(`${apiUrl}/hubs/telemetry`, { accessTokenFactory: () => session.accessToken })
  .configureLogging(LogLevel.Warning)
  .build()

const reading = new Promise((resolve, reject) => {
  const timeout = setTimeout(() => reject(new Error('No se recibió telemetryReadingReceived en 12 segundos.')), 12_000)
  connection.on('telemetryReadingReceived', payload => {
    clearTimeout(timeout)
    resolve(payload)
  })
})

await connection.start()
const payload = await reading
console.log(JSON.stringify({ event: 'telemetryReadingReceived', sensorName: payload.sensorName, value: payload.value, transport: payload.transport }))
await connection.stop()
