namespace SistemaRiego.Api.Models;
public static class RoleNames { public const string Administrator = "Administrador"; public const string Technician = "Tecnico"; public const string Operator = "Operador"; }
public static class Policies { public const string Administrator = "SoloAdministrador"; public const string Technician = "TecnicoOSuperior"; public const string Operator = "UsuarioOperativo"; }
public sealed class JwtOptions
{
    public const string SectionName = "Jwt"; public required string Issuer { get; init; } public required string Audience { get; init; } public required string SigningKey { get; init; }
    public int AccessTokenMinutes { get; init; } = 15; public int RefreshTokenDays { get; init; } = 7; public int PasswordRecoveryMinutes { get; init; } = 30;
}
