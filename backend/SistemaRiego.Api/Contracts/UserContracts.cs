using SistemaRiego.Api.Models;
namespace SistemaRiego.Api.Contracts;
public sealed record UpdateUserProfileRequest([System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MinLength(3), System.ComponentModel.DataAnnotations.MaxLength(150)] string FullName, [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.EmailAddress] string Email);
public sealed record UpdateUserStatusRequest(UserStatus Status); public sealed record UpdateUserRolesRequest(IReadOnlyCollection<string> Roles);
public sealed record AdminPasswordResetResponse(string TemporaryPassword);
public sealed record RoleResponse(string Name, string Description, IReadOnlyCollection<string> Permissions);

public sealed record PermissionResponse(Guid Id, string Code, string Description);
public sealed record UpdateRolePermissionsRequest(IReadOnlyCollection<string> Permissions);
