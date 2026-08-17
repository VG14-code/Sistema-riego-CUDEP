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
client.ApplicationMessageReceivedAsync += async message =>
{
    if (!message.ApplicationMessage.Topic.EndsWith("/comando", StringComparison.Ordinal)) return;
    var command = message.ApplicationMessage.ConvertPayloadToString();
    await Task.Delay(config.AckDelayMilliseconds);
    var ackTopic = message.ApplicationMessage.Topic[..^"/comando".Length] + "/ack";
    var ack = JsonSerializer.Serialize(new { status = CommandStatus(command), command, acknowledgedAtUtc = DateTime.UtcNow }, JsonOptions());
    await client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic(ackTopic).WithPayload(ack)
        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build());
    Console.WriteLine($"ACK {ackTopic}: {ack}");
};

await client.ConnectAsync(options);
var subscription = factory.CreateSubscribeOptionsBuilder()
    .WithTopicFilter(filter => filter.WithTopic("granja/+/valvula/+/comando").WithAtLeastOnceQoS()).Build();
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
        await Task.Delay(TimeSpan.FromSeconds(config.IntervalSeconds), shutdown.Token);
    }
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
finally
{
    if (client.IsConnected) await client.DisconnectAsync();
}

static JsonSerializerOptions JsonOptions() => new(JsonSerializerDefaults.Web) { WriteIndented = false, PropertyNameCaseInsensitive = true };
static string CommandStatus(string payload) => payload.Contains("cerr", StringComparison.OrdinalIgnoreCase) ? "válvula cerrada" : "válvula abierta";

sealed record SimulatorConfig(string BrokerHost, int BrokerPort, string ClientId, string Username, string Password, int IntervalSeconds, int AckDelayMilliseconds, int RandomSeed, IReadOnlyList<ZoneConfig> Zones);
sealed record ZoneConfig(string Code, string TopicKey, IReadOnlyList<SensorConfig> Sensors);
sealed record SensorConfig(string Code, decimal Minimum, decimal Maximum, decimal MaximumVariation);
sealed class SensorState(ZoneConfig zone, SensorConfig sensor, decimal value)
{
    public ZoneConfig Zone { get; } = zone;
    public SensorConfig Sensor { get; } = sensor;
    public decimal Value { get; set; } = value;
}
