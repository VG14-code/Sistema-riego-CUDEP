using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Tests;

public sealed class RemoteConfigurationServiceTests
{
    [Fact]
    public async Task Dispatch_PublishesToNodeAndMarksCommandAsSent()
    {
        await using var db = Db();
        var node = Node();
        var command = new RemoteConfigurationCommand { Node = node, CommandType = "CAMBIAR_FRECUENCIA", Payload = "{\"intervalSeconds\":30}" };
        db.Add(command);
        await db.SaveChangesAsync();
        var mqtt = new Publisher();
        var service = new RemoteConfigurationDispatcher(db, mqtt, NullLogger<RemoteConfigurationDispatcher>.Instance);

        await service.DispatchAsync(command.Id, default);

        Assert.Equal("Enviada", command.Status);
        Assert.Equal(1, command.Attempts);
        Assert.Equal(command.Id, mqtt.CommandId);
        Assert.Equal("RPI-CUDEP-01", mqtt.NodeCode);
        Assert.Equal(30, mqtt.Payload.GetProperty("intervalSeconds").GetInt32());
    }

    [Fact]
    public async Task Acknowledge_ConfirmsOnlyTheCorrelatedNode()
    {
        await using var db = Db();
        var command = new RemoteConfigurationCommand { Node = Node(), CommandType = "REINICIAR", Payload = "{}", Status = "Enviada", Attempts = 1 };
        db.Add(command);
        await db.SaveChangesAsync();
        var service = new RemoteConfigurationDispatcher(db, new Publisher(), NullLogger<RemoteConfigurationDispatcher>.Instance);

        Assert.False(await service.AcknowledgeAsync(command.Id, "OTRO-NODO", true, null, default));
        Assert.True(await service.AcknowledgeAsync(command.Id, "rpi-cudep-01", true, "Reiniciado", default));

        Assert.Equal("Confirmada", command.Status);
        Assert.NotNull(command.ConfirmedAtUtc);
    }

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static IoTNode Node() => new() { Code = "RPI-CUDEP-01", Name = "Gateway", OperationalStatus = new MasterCatalogItem { Kind = CatalogKind.OperationalStatus, Code = "ACTIVE", Name = "Activo" } };

    private sealed class Publisher : IMqttCommandPublisher
    {
        public Guid CommandId { get; private set; }
        public string? NodeCode { get; private set; }
        public JsonElement Payload { get; private set; }
        public Task PublishCommandAsync(string zone, Guid deviceId, object payload, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PublishRemoteConfigurationAsync(string nodeCode, Guid commandId, string commandType, JsonElement payload, CancellationToken cancellationToken)
        {
            NodeCode = nodeCode;
            CommandId = commandId;
            Payload = payload;
            return Task.CompletedTask;
        }
    }
}
