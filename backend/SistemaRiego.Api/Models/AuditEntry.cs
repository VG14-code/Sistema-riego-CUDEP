namespace SistemaRiego.Api.Models;

public sealed class AuditEntry
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public required string ActionType { get; set; }
    public required string EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? Detail { get; set; }
    public string? IpAddress { get; set; }
    public required string CorrelationId { get; set; }
    public string Origin { get; set; } = "API";
}
