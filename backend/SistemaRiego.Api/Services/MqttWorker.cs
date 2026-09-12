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
    public string PumpAckTopic { get; init; } = "granja/estacion/bomba/+/ack";
    public string StationTelemetryTopic { get; init; } = "granja/estacion/telemetria";
    public string EnergyTelemetryTopic { get; init; } = "granja/energia/telemetria";
    public string FlowTelemetryTopic { get; init; } = "granja/+/caudal/lectura";
    public string PumpCommandTopicTemplate { get; init; } = "granja/estacion/bomba/{device}/comando";
    public string RemoteConfigurationCommandTopicTemplate { get; init; } = "granja/nodo/{node}/configuracion/comando";
    public string RemoteConfigurationAckTopic { get; init; } = "granja/nodo/+/configuracion/ack";
    public int ReconnectSeconds { get; init; } = 5;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public Dictionary<string, string> AllowedClients { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>
/// El broker no acepto la publicacion porque la conexion no existe. Se distingue de
/// otros fallos para no gastar intentos de un comando que ni siquiera salio.
/// </summary>
public sealed class MqttBrokerUnavailableException(Exception? inner = null)
    : InvalidOperationException("El broker MQTT no está disponible.", inner);

public interface IMqttCommandPublisher
{
    Task PublishCommandAsync(string zone, Guid deviceId, object payload, CancellationToken cancellationToken);
    Task PublishPumpCommandAsync(Guid deviceId, object payload, CancellationToken cancellationToken) => Task.CompletedTask;
    Task PublishRemoteConfigurationAsync(string nodeCode, Guid commandId, string commandType, JsonElement payload, CancellationToken cancellationToken) => Task.CompletedTask;
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
                        .WithTopicFilter(f => f.WithTopic(options.PumpAckTopic).WithAtLeastOnceQoS())
                        .WithTopicFilter(f => f.WithTopic(options.StationTelemetryTopic).WithAtLeastOnceQoS())
                        .WithTopicFilter(f => f.WithTopic(options.EnergyTelemetryTopic).WithAtLeastOnceQoS())
                        .WithTopicFilter(f => f.WithTopic(options.FlowTelemetryTopic).WithAtLeastOnceQoS())
                        .WithTopicFilter(f => f.WithTopic(options.RemoteConfigurationAckTopic).WithAtLeastOnceQoS())
                        .Build();
                    await client.SubscribeAsync(subscribe, stoppingToken);
                    logger.LogInformation("MQTT conectado a {Host}:{Port}; suscrito a {TelemetryTopic} y {AckTopic}", options.Host, options.Port, options.TelemetryTopic, options.AckTopic);
                    using var reconcileScope = scopeFactory.CreateScope();
                    await reconcileScope.ServiceProvider.GetRequiredService<IIrrigationCommandService>().ReconcileAsync(stoppingToken);
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
            if (args.ApplicationMessage.Topic.EndsWith("/configuracion/ack", StringComparison.Ordinal))
            {
                await OnRemoteConfigurationAckAsync(args);
                return;
            }
            if (args.ApplicationMessage.Topic.EndsWith("/ack", StringComparison.Ordinal))
            {
                await OnAckAsync(args);
                return;
            }

            var payload = args.ApplicationMessage.ConvertPayloadToString();
            using var scope = scopeFactory.CreateScope();
            var sprint4 = scope.ServiceProvider.GetRequiredService<ISprint4TelemetryService>();
            if (args.ApplicationMessage.Topic == options.StationTelemetryTopic) { await sprint4.IngestStationAsync(JsonSerializer.Deserialize<PumpStationTelemetry>(payload, json) ?? throw new JsonException("Telemetría de estación vacía."), CancellationToken.None); return; }
            if (args.ApplicationMessage.Topic == options.EnergyTelemetryTopic) { await sprint4.IngestEnergyAsync(JsonSerializer.Deserialize<EnergyTelemetry>(payload, json) ?? throw new JsonException("Telemetría energética vacía."), CancellationToken.None); return; }
            if (args.ApplicationMessage.Topic.EndsWith("/caudal/lectura", StringComparison.Ordinal)) { await sprint4.IngestFlowAsync(JsonSerializer.Deserialize<FlowTelemetry>(payload, json) ?? throw new JsonException("Telemetría de caudal vacía."), CancellationToken.None); return; }
            var envelope = JsonSerializer.Deserialize<MqttTelemetryEnvelope>(payload, json)
                ?? throw new JsonException("El payload MQTT está vacío.");
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
        var commandId = args.ApplicationMessage.Topic.Contains("/bomba/", StringComparison.Ordinal)
            ? await scope.ServiceProvider.GetRequiredService<PumpAckService>().ProcessAsync(deviceId, args.ApplicationMessage.ConvertPayloadToString(), CancellationToken.None)
            : await scope.ServiceProvider.GetRequiredService<IrrigationAckService>().ProcessAsync(deviceId, args.ApplicationMessage.ConvertPayloadToString(), CancellationToken.None);
        if (commandId is null) logger.LogWarning("ACK sin comando correlacionable en {Topic}: {Payload}", args.ApplicationMessage.Topic, args.ApplicationMessage.ConvertPayloadToString());
        else logger.LogInformation("ACK MQTT confirmó el comando {CommandId} en {Topic}", commandId, args.ApplicationMessage.Topic);
    }

    private async Task OnRemoteConfigurationAckAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var segments = args.ApplicationMessage.Topic.Split('/');
        if (segments.Length != 5) throw new JsonException($"Tópico de configuración inválido: {args.ApplicationMessage.Topic}.");
        var payload = JsonSerializer.Deserialize<RemoteConfigurationAckEnvelope>(args.ApplicationMessage.ConvertPayloadToString(), json)
            ?? throw new JsonException("ACK de configuración vacío.");
        using var scope = scopeFactory.CreateScope();
        var accepted = await scope.ServiceProvider.GetRequiredService<IRemoteConfigurationDispatcher>()
            .AcknowledgeAsync(payload.CommandId, segments[2], payload.Success, payload.Detail, CancellationToken.None);
        if (!accepted) logger.LogWarning("ACK de configuración sin comando correlacionable en {Topic}", args.ApplicationMessage.Topic);
        else logger.LogInformation("ACK MQTT confirmó la configuración remota {CommandId}", payload.CommandId);
    }
    public async Task PublishCommandAsync(string zone, Guid deviceId, object payload, CancellationToken cancellationToken)
    {
        if (!client.IsConnected) throw new MqttBrokerUnavailableException();
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


    public async Task PublishRemoteConfigurationAsync(string nodeCode, Guid commandId, string commandType, JsonElement payload, CancellationToken cancellationToken)
    {
        if (!client.IsConnected) throw new MqttBrokerUnavailableException();
        var topic = options.RemoteConfigurationCommandTopicTemplate.Replace("{node}", Normalize(nodeCode), StringComparison.Ordinal);
        var message = new MqttApplicationMessageBuilder().WithTopic(topic)
            .WithPayload(JsonSerializer.Serialize(new { commandId, commandType, payload }, json))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build();
        // La conexion puede caer entre la comprobacion y el envio: si tras el fallo el
        // cliente ya no esta conectado, fue el broker y no el comando.
        try { await client.PublishAsync(message, cancellationToken); }
        catch (Exception exception) when (exception is not OperationCanceledException && !client.IsConnected) { throw new MqttBrokerUnavailableException(exception); }
        logger.LogInformation("Configuración remota {CommandId} publicada en {Topic}", commandId, topic);
    }
    public async Task PublishPumpCommandAsync(Guid deviceId, object payload, CancellationToken cancellationToken)
    {
        if (!client.IsConnected) throw new MqttBrokerUnavailableException();
        var topic = options.PumpCommandTopicTemplate.Replace("{device}", deviceId.ToString(), StringComparison.Ordinal);
        var message = new MqttApplicationMessageBuilder().WithTopic(topic).WithPayload(JsonSerializer.Serialize(payload, json)).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build();
        await client.PublishAsync(message, cancellationToken);
        logger.LogInformation("Comando de bomba MQTT publicado en {Topic}", topic);
    }

    public sealed record RemoteConfigurationAckEnvelope(Guid CommandId, bool Success, string? Detail);
    public sealed record MqttTelemetryEnvelope(Guid? SensorId, string? SensorCode, Guid? IrrigationZoneId,
    string? IrrigationZoneCode, DateTime? CapturedAtUtc, decimal Value, decimal? BatteryPercent, int? SignalStrength, string MessageId, bool IsSimulated = false);
    private static string Normalize(string value) => string.Concat(value.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-'));
}
