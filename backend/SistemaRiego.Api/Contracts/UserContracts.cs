using SistemaRiego.Api.Models;
namespace SistemaRiego.Api.Contracts;
public sealed record UpdateUserProfileRequest([System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MinLength(3), System.ComponentModel.DataAnnotations.MaxLength(150)] string FullName, [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.EmailAddress] string Email, [System.ComponentModel.DataAnnotations.MaxLength(50)] string? PersonnelCode = null, Guid? UniversityCenterId = null, Guid? FarmId = null);
// La pantalla envia el nombre del estado (Active, Blocked, Disabled); el enlace de
// modelos solo aceptaba el numero y devolvia 400, asi que el bloqueo no funcionaba.
public sealed record UpdateUserStatusRequest(string Status); public sealed record UpdateUserRolesRequest(IReadOnlyCollection<string> Roles);
public sealed record AdminPasswordResetResponse(string TemporaryPassword);
public sealed record RoleResponse(Guid Id, string Name, string Description, bool IsActive, int UserCount, IReadOnlyCollection<string> Permissions);
public sealed record SaveRoleRequest([System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MinLength(3), System.ComponentModel.DataAnnotations.MaxLength(80)] string Name, [System.ComponentModel.DataAnnotations.MaxLength(250)] string Description, bool IsActive = true);
public sealed record UpdateRoleStatusRequest(bool IsActive);

public sealed record PermissionResponse(Guid Id, string Code, string Description);
public sealed record UpdateRolePermissionsRequest(IReadOnlyCollection<string> Permissions);
