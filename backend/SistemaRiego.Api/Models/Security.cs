namespace SistemaRiego.Api.Models;
public static class RoleNames { public const string Administrator = "Administrador"; public const string Technician = "Tecnico"; public const string Operator = "Operador"; }
public static class Policies { public const string Administrator = "SoloAdministrador"; public const string Technician = "TecnicoOSuperior"; public const string Operator = "UsuarioOperativo"; }
public static class PermissionPolicies
{
    public const string DeviceCatalogsRead = "Permiso:DispositivosCatalogosLeer";
    public const string DeviceCatalogsManage = "Permiso:DispositivosCatalogosGestionar";
    public const string DeviceCatalogsDelete = "Permiso:DispositivosCatalogosEliminar";
}
public sealed class JwtOptions
{
    public const string SectionName = "Jwt"; public required string Issuer { get; init; } public required string Audience { get; init; } public required string SigningKey { get; init; }
    public int AccessTokenMinutes { get; init; } = 15; public int RefreshTokenDays { get; init; } = 7; public int PasswordRecoveryMinutes { get; init; } = 30;
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public string Provider { get; init; } = "File";
    public string FromAddress { get; init; } = "vgbm123456@gmail.com";
    public string FromName { get; init; } = "Sistema de Riego CUDEP";
    public string FrontendBaseUrl { get; init; } = "http://localhost:5173";
    public string FileDirectory { get; init; } = "dev-mailbox";
    public string SmtpHost { get; init; } = "smtp.gmail.com";
    public int SmtpPort { get; init; } = 587;
    public string SmtpUsername { get; init; } = "vgbm123456@gmail.com";
    public string? SmtpPassword { get; init; }
    public bool EnableSsl { get; init; } = true;
    public int SmtpTimeoutSeconds { get; init; } = 15;
    public bool ArchiveSentMessages { get; init; }
}