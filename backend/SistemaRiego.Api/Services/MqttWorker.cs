using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;
using MQTTnet;
using MQTTnet.Protocol;
using SistemaRiego.Api.Contracts;

namespace SistemaRiego.Api.Services;

public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 1883;
    public string ClientId { get; init; } = "sistema-riego-api";
    public string TelemetryTopic { get; init; } = "granja/+/sensor/+/lectura";
    public string CommandTopicTemplate { get; init; } = "granja/{zone}/valvula/{device}/comando";
    public string AckTopic { get; init; } = "granja/+/valvula/+/ack";
    public int ReconnectSeconds { get; init; } = 5;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public Dictionary<string, string> AllowedClients { get; init; } = new(StringComparer.Ordinal);
}

public interface IMqttCommandPublisher
{
    Task PublishCommandAsync(string zone, Guid deviceId, object payload, CancellationToken cancellationToken);
}

public sealed class MqttWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<MqttOptions> mqttOptions,
    ILogger<MqttWorker> logger) : BackgroundService, IMqttCommandPublisher
{
    private readonly MqttOptions options = mqttOptions.Value;
    private readonly MqttClientFactory factory = new();
    private readonly IMqttClient client = new MqttClientFactory().CreateMqttClient();
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        client.ApplicationMessageReceivedAsync += OnMessageAsync;
        var clientOptionsBuilder = new MqttClientOptionsBuilder()
            .WithClientId(options.ClientId)
            .WithTcpServer(options.Host, options.Port)
            .WithCleanSession();
        if (!string.IsNullOrWhiteSpace(options.Username))
            clientOptionsBuilder.WithCredentials(options.Username, options.Password);
        var clientOptions = clientOptionsBuilder.Build();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!client.IsConnected)
                {
                    await client.ConnectAsync(clientOptions, stoppingToken);
                    var subscribe = factory.CreateSubscribeOptionsBuilder()
                        .WithTopicFilter(f => f.WithTopic(options.TelemetryTopic).WithAtLeastOnceQoS())
                        .WithTopicFilter(f => f.WithTopic(options.AckTopic).WithAtLeastOnceQoS())
                        .Build();
                    await client.SubscribeAsync(subscribe, stoppingToken);
                    logger.LogInformation("MQTT conectado a {Host}:{Port}; suscrito a {TelemetryTopic} y {AckTopic}", options.Host, options.Port, options.TelemetryTopic, options.AckTopic);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "No fue posible conectar con MQTT; nuevo intento en {Seconds}s", options.ReconnectSeconds);
            }

            await Task.Delay(TimeSpan.FromSeconds(options.ReconnectSeconds), stoppingToken);
        }
    }

    private async Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        try
        {
            if (args.ApplicationMessage.Topic.EndsWith("/ack", StringComparison.Ordinal))
            {
                await OnAckAsync(args);
                return;
            }

            var payload = args.ApplicationMessage.ConvertPayloadToString();
            var envelope = JsonSerializer.Deserialize<MqttTelemetryEnvelope>(payload, json)
                ?? throw new JsonException("El payload MQTT está vacío.");
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sensorId = envelope.SensorId ?? await db.IoTSensors.Where(x => x.Code == envelope.SensorCode && x.IsActive).Select(x => (Guid?)x.Id).SingleOrDefaultAsync()
                ?? throw new JsonException($"Sensor desconocido: {envelope.SensorCode}.");
            var zoneId = envelope.IrrigationZoneId;
            if (!zoneId.HasValue && !string.IsNullOrWhiteSpace(envelope.IrrigationZoneCode))
                zoneId = await db.IrrigationZones.Where(x => x.Code == envelope.IrrigationZoneCode && x.IsActive).Select(x => (Guid?)x.Id).SingleOrDefaultAsync();
            var transport = envelope.IsSimulated ? "MQTT_SIMULATED" : "MQTT";
            var request = new TelemetryRequest(sensorId, zoneId, envelope.CapturedAtUtc, envelope.Value,
                envelope.BatteryPercent, envelope.SignalStrength, envelope.MessageId, transport);
            var ingestion = scope.ServiceProvider.GetRequiredService<ITelemetryIngestionService>();
            var result = await ingestion.IngestAsync(request, transport, CancellationToken.None);
            logger.LogInformation("Trama MQTT {MessageId} procesada con estado {Status}", request.MessageId, result.Status);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Trama MQTT inválida recibida en {Topic}", args.ApplicationMessage.Topic);
        }
    }

    private async Task OnAckAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var segments = args.ApplicationMessage.Topic.Split('/');
        if (segments.Length != 5 || !Guid.TryParse(segments[3], out var deviceId))
            throw new JsonException($"Tópico ACK inválido: {args.ApplicationMessage.Topic}.");

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var command = await db.IoTCommands
            .Where(x => x.DeviceId == deviceId && x.Status != "Confirmado")
            .OrderByDescending(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync();
        if (command is null)
        {
            logger.LogWarning("ACK sin comando pendiente en {Topic}: {Payload}", args.ApplicationMessage.Topic, args.ApplicationMessage.ConvertPayloadToString());
            return;
        }

        command.Status = "Confirmado";
        command.ConfirmedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        logger.LogInformation("ACK MQTT confirmó el comando {CommandId} en {Topic}: {Payload}", command.Id, args.ApplicationMessage.Topic, args.ApplicationMessage.ConvertPayloadToString());
    }

    public async Task PublishCommandAsync(string zone, Guid deviceId, object payload, CancellationToken cancellationToken)
    {
        if (!client.IsConnected) throw new InvalidOperationException("El broker MQTT no está disponible.");
        var topic = options.CommandTopicTemplate
            .Replace("{zone}", Normalize(zone), StringComparison.Ordinal)
            .Replace("{device}", deviceId.ToString(), StringComparison.Ordinal);
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(JsonSerializer.Serialize(payload, json))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();
        await client.PublishAsync(message, cancellationToken);
        logger.LogInformation("Comando MQTT publicado en {Topic}", topic);
    }


public sealed record MqttTelemetryEnvelope(Guid? SensorId, string? SensorCode, Guid? IrrigationZoneId,
    string? IrrigationZoneCode, DateTime? CapturedAtUtc, decimal Value, decimal? BatteryPercent, int? SignalStrength, string MessageId, bool IsSimulated = false);
    private static string Normalize(string value) => string.Concat(value.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-'));
}
