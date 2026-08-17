using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace SistemaRiego.Api.Models;

public enum UserStatus { Active = 1, Blocked = 2, Disabled = 3 }

public sealed class User : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public PasswordCredential? Credential { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<Session> Sessions { get; set; } = [];
}

// Conservado durante la migración para trasladar hashes de cuentas existentes a Identity.
public sealed class PasswordCredential
{
    public Guid UserId { get; set; }
    public required string PasswordHash { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public User User { get; set; } = null!;
}

public sealed class Role : IdentityRole<Guid>
{
    public string Description { get; set; } = string.Empty;
    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}

public sealed class Permission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string Description { get; set; }
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}

public sealed class UserRole : IdentityUserRole<Guid>
{
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}

public sealed class RolePermission
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    public Role Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}

public sealed class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string RefreshTokenHash { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Guid? ReplacedBySessionId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public User User { get; set; } = null!;
    [NotMapped] public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}

public sealed class PasswordRecoveryToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public User User { get; set; } = null!;
}

public sealed class AccessAudit
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public required string EventType { get; set; }
    public required string Detail { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
}
