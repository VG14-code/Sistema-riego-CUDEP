namespace SistemaRiego.Api.Contracts;

public sealed record EffectivePermissionResponse(string Code, string Description, bool GrantedByRole, bool? OverrideIsGranted, bool EffectiveGranted);
public sealed record PermissionOverrideRequest(string Code, bool IsGranted);
public sealed record UpdateUserPermissionsRequest(IReadOnlyCollection<PermissionOverrideRequest> Overrides);
