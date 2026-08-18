using System.Text.Json;
using MQTTnet;
using MQTTnet.Protocol;

var configPath = args.FirstOrDefault() ?? Path.Combine(AppContext.BaseDirectory, "simulator-settings.json");
if (!File.Exists(configPath)) configPath = Path.Combine(Directory.GetCurrentDirectory(), "simulator-settings.json");
var config = JsonSerializer.Deserialize<SimulatorConfig>(await File.ReadAllTextAsync(configPath), JsonOptions())
    ?? throw new InvalidOperationException("No fue posible leer la configuración del simulador.");

var mqttPassword = Environment.GetEnvironmentVariable("SISTEMA_RIEGO_MQTT_PASSWORD") ?? config.Password;
if (string.IsNullOrWhiteSpace(mqttPassword)) throw new InvalidOperationException("Defina SISTEMA_RIEGO_MQTT_PASSWORD para autenticar el simulador.");

var factory = new MqttClientFactory();
using var client = factory.CreateMqttClient();
var options = new MqttClientOptionsBuilder()
    .WithClientId(config.ClientId)
    .WithTcpServer(config.BrokerHost, config.BrokerPort)
    .WithCredentials(config.Username, mqttPassword)
    .WithCleanSession()
    .Build();
var valveStates = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
var pumpRunning = false;
var tankLevelLiters = decimal.TryParse(Environment.GetEnvironmentVariable("SIMULATOR_TANK_LEVEL_LITERS"), out var configuredTankLevel) ? configuredTankLevel : 7200m;
var forceOvercurrent = bool.TryParse(Environment.GetEnvironmentVariable("SIMULATE_PUMP_OVERCURRENT"), out var configuredOvercurrent) && configuredOvercurrent;
client.ApplicationMessageReceivedAsync += async message =>
{
    if (!message.ApplicationMessage.Topic.EndsWith("/comando", StringComparison.Ordinal)) return;
    var payload = message.ApplicationMessage.ConvertPayloadToString();
    using var document = JsonDocument.Parse(payload);
    var root = document.RootElement;
    var commandId = root.TryGetProperty("commandId", out var idValue) && Guid.TryParse(idValue.ToString(), out var parsedId) ? parsedId : (Guid?)null;
    var commandType = root.TryGetProperty("commandType", out var typeValue) ? typeValue.GetString() ?? string.Empty : payload;
    var segments = message.ApplicationMessage.Topic.Split('/');
    var deviceId = segments.Length > 3 ? segments[3] : "unknown";
    var isPumpCommand = message.ApplicationMessage.Topic.Contains("/bomba/", StringComparison.Ordinal);
    if (isPumpCommand && commandType.Contains("ENCENDER", StringComparison.OrdinalIgnoreCase)) pumpRunning = true;
    else if (isPumpCommand && commandType.Contains("APAGAR", StringComparison.OrdinalIgnoreCase)) pumpRunning = false;
    else if (commandType.Contains("ABRIR", StringComparison.OrdinalIgnoreCase)) valveStates[deviceId] = true;
    else if (commandType.Contains("CERRAR", StringComparison.OrdinalIgnoreCase)) valveStates[deviceId] = false;
    var isOpen = valveStates.GetValueOrDefault(deviceId);
    await Task.Delay(config.AckDelayMilliseconds);
    var ackTopic = message.ApplicationMessage.Topic[..^"/comando".Length] + "/ack";
    var ack = JsonSerializer.Serialize(new { commandId, commandType, status = isPumpCommand ? (pumpRunning ? "bomba encendida" : "bomba detenida") : (isOpen ? "válvula abierta" : "válvula cerrada"), isOpen, isRunning = pumpRunning, acknowledgedAtUtc = DateTime.UtcNow }, JsonOptions());
    await client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic(ackTopic).WithPayload(ack)
        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build());
    Console.WriteLine($"ACK {ackTopic}: {ack}");
};

await client.ConnectAsync(options);
var subscription = factory.CreateSubscribeOptionsBuilder()
    .WithTopicFilter(filter => filter.WithTopic("granja/+/valvula/+/comando").WithAtLeastOnceQoS())
    .WithTopicFilter(filter => filter.WithTopic("granja/estacion/bomba/+/comando").WithAtLeastOnceQoS()).Build();
await client.SubscribeAsync(subscription);
Console.WriteLine($"Simulador {config.ClientId} conectado a {config.BrokerHost}:{config.BrokerPort}. Ctrl+C para detener.");

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; shutdown.Cancel(); };
var random = new Random(config.RandomSeed);
var values = config.Zones.SelectMany(zone => zone.Sensors.Select(sensor => new SensorState(zone, sensor, (sensor.Minimum + sensor.Maximum) / 2))).ToList();

try
{
    while (!shutdown.IsCancellationRequested)
    {
        foreach (var state in values)
        {
            state.Value = Math.Clamp(state.Value + (decimal)((random.NextDouble() * 2 - 1) * (double)state.Sensor.MaximumVariation), state.Sensor.Minimum, state.Sensor.Maximum);
            var payload = JsonSerializer.Serialize(new
            {
                sensorCode = state.Sensor.Code,
                irrigationZoneCode = state.Zone.Code,
                capturedAtUtc = DateTime.UtcNow,
                value = Math.Round(state.Value, 2),
                batteryPercent = 90 + random.Next(0, 10),
                signalStrength = -45 - random.Next(0, 25),
                messageId = $"VIRTUAL-{state.Sensor.Code}-{Guid.NewGuid():N}",
                isSimulated = true
            }, JsonOptions());
            var topic = $"granja/{state.Zone.TopicKey}/sensor/{state.Sensor.Code.ToLowerInvariant()}/lectura";
            await client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic(topic).WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build(), shutdown.Token);
            Console.WriteLine($"{DateTime.Now:T} {topic}: {state.Value:0.00}");
        }
        foreach (var zone in config.Zones)
        {
            var open = valveStates.Values.Count(x => x);
            var flow = open == 0 ? 0m : Math.Round(12m * open + (decimal)(random.NextDouble() - .5), 2);
            var flowPayload = JsonSerializer.Serialize(new { irrigationZoneCode = zone.Code, capturedAtUtc = DateTime.UtcNow, flowLitersMinute = flow, messageId = $"FLOW-{zone.Code}-{Guid.NewGuid():N}" }, JsonOptions());
            await client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic($"granja/{zone.TopicKey}/caudal/lectura").WithPayload(flowPayload).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build(), shutdown.Token);
        }
        tankLevelLiters = Math.Clamp(tankLevelLiters + (pumpRunning ? 8m : -2m), 0, 10000);
        var motorCurrent = pumpRunning ? forceOvercurrent ? 18m : Math.Round(7m + (decimal)random.NextDouble() * 1.5m, 2) : 0m;
        var stationPayload = JsonSerializer.Serialize(new { pumpCode = "BOMBA-ABAST-01", capturedAtUtc = DateTime.UtcNow, levelLiters = tankLevelLiters, pressureBar = pumpRunning ? 3.2m : 2.4m, motorCurrentAmps = motorCurrent, isPumpRunning = pumpRunning, messageId = $"STATION-{Guid.NewGuid():N}" }, JsonOptions());
        await client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic("granja/estacion/telemetria").WithPayload(stationPayload).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build(), shutdown.Token);
        var daylight = DateTime.Now.Hour is >= 6 and <= 18; var generation = daylight ? 900 + random.Next(0, 500) : 0;
        var energyPayload = JsonSerializer.Serialize(new { capturedAtUtc = DateTime.UtcNow, generationWatts = generation, batteryPercent = 78 + random.Next(0, 8), consumptionWatts = 180 + (pumpRunning ? 650 : 0), batteryVoltage = 50.8m, messageId = $"ENERGY-{Guid.NewGuid():N}" }, JsonOptions());
        await client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic("granja/energia/telemetria").WithPayload(energyPayload).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build(), shutdown.Token);
        await Task.Delay(TimeSpan.FromSeconds(config.IntervalSeconds), shutdown.Token);
    }
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
finally
{
    if (client.IsConnected) await client.DisconnectAsync();
}

static JsonSerializerOptions JsonOptions() => new(JsonSerializerDefaults.Web) { WriteIndented = false, PropertyNameCaseInsensitive = true };

sealed record SimulatorConfig(string BrokerHost, int BrokerPort, string ClientId, string Username, string Password, int IntervalSeconds, int AckDelayMilliseconds, int RandomSeed, IReadOnlyList<ZoneConfig> Zones);
sealed record ZoneConfig(string Code, string TopicKey, IReadOnlyList<SensorConfig> Sensors);
sealed record SensorConfig(string Code, decimal Minimum, decimal Maximum, decimal MaximumVariation);
sealed class SensorState(ZoneConfig zone, SensorConfig sensor, decimal value)
{
    public ZoneConfig Zone { get; } = zone;
    public SensorConfig Sensor { get; } = sensor;
    public decimal Value { get; set; } = value;
}
