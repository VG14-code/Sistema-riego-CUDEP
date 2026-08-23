namespace SistemaRiego.Api.Services;

public interface IPermissionResolver
{
    Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct);
}
