using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Controllers;

[ApiController, Route("api/iot/communication"), Authorize(Policy = Policies.Operator)]
public sealed class IoTCommunicationController(IOptions<MqttOptions> mqttOptions) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var options = mqttOptions.Value;
        return Ok(new
        {
            options.Host,
            options.Port,
            Protocol = "MQTT 3.1.1 / TCP",
            Topics = new { options.TelemetryTopic, options.CommandTopicTemplate, options.AckTopic, options.StationTelemetryTopic, options.EnergyTelemetryTopic, options.FlowTelemetryTopic },
            Authentication = options.AllowedClients.Count > 0 ? "Credenciales por cliente" : "Sin clientes configurados",
            AllowedClientIds = options.AllowedClients.Keys.OrderBy(x => x).ToArray(),
            SecretsExposed = false
        });
    }
}