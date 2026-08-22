using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Hubs;

[Authorize(Policy = Policies.Operator)]
public sealed class TelemetryHub : Hub
{
    public const string ReadingReceived = "telemetryReadingReceived";
    public const string AlertRaised = "alertRaised";
    public const string AlertResolved = "alertResolved";
}

