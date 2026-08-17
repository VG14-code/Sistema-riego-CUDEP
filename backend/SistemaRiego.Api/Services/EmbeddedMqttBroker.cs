using Microsoft.Extensions.Options;
using MQTTnet.Server;
using MQTTnet.Protocol;

namespace SistemaRiego.Api.Services;

/// <summary>
/// Broker local para desarrollo. En otros entornos el cliente usa el broker externo configurado.
/// </summary>
public sealed class EmbeddedMqttBroker(
    IOptions<MqttOptions> mqttOptions,
    ILogger<EmbeddedMqttBroker> logger) : IHostedService
{
    private readonly MqttOptions options = mqttOptions.Value;
    private readonly MqttServerFactory factory = new();
    private MqttServer? server;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var serverOptions = factory.CreateServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(options.Port)
            .Build();

        server = factory.CreateMqttServer(serverOptions);
        server.ValidatingConnectionAsync += args =>
        {
            var authenticated = !string.IsNullOrWhiteSpace(args.UserName)
                && string.Equals(args.ClientId, args.UserName, StringComparison.Ordinal)
                && options.AllowedClients.TryGetValue(args.UserName, out var expectedPassword)
                && string.Equals(expectedPassword, args.Password, StringComparison.Ordinal);
            args.ReasonCode = authenticated ? MqttConnectReasonCode.Success : MqttConnectReasonCode.BadUserNameOrPassword;
            if (!authenticated)
                logger.LogWarning("Conexión MQTT rechazada para ClientId {ClientId}", args.ClientId);
            return Task.CompletedTask;
        };
        await server.StartAsync();
        logger.LogInformation("Broker MQTT embebido iniciado en {Host}:{Port} (solo Development)", options.Host, options.Port);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (server is null)
            return;

        await server.StopAsync();
        logger.LogInformation("Broker MQTT embebido detenido");
    }

}
