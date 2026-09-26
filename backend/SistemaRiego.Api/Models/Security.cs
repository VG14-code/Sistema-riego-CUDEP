namespace SistemaRiego.Api.Models;
public static class RoleNames { public const string Administrator = "Administrador"; public const string Technician = "Tecnico"; public const string Operator = "Operador"; }
public static class Policies { public const string Administrator = "SoloAdministrador"; public const string Technician = "TecnicoOSuperior"; public const string Operator = "UsuarioOperativo"; }
public static class PermissionPolicies
{
    public const string DeviceCatalogsRead = "Permiso:DispositivosCatalogosLeer";
    public const string DeviceCatalogsManage = "Permiso:DispositivosCatalogosGestionar";
    public const string DeviceCatalogsDelete = "Permiso:DispositivosCatalogosEliminar";

    public const string IrrigationOperate = "Permiso:RiegoOperar";
    public const string UsersManage = "Permiso:UsuariosGestionar";
    public const string ReportsRead = "Permiso:ReportesLeer";

    public const string AgronomyRead = "Permiso:AgronomiaLeer";
    public const string AgronomyManage = "Permiso:AgronomiaGestionar";
    public const string AgronomyDelete = "Permiso:AgronomiaEliminar";

    public const string EnergyRead = "Permiso:EnergiaLeer";
    public const string EnergyManage = "Permiso:EnergiaGestionar";

    public const string AlertsRead = "Permiso:AlertasLeer";
    public const string AlertsManage = "Permiso:AlertasGestionar";

    public const string AutomationRead = "Permiso:AutomatizacionLeer";
    public const string AutomationManage = "Permiso:AutomatizacionGestionar";

    public const string CropPlanningRead = "Permiso:PlanificacionLeer";
    public const string CropPlanningManage = "Permiso:PlanificacionGestionar";
    public const string CropPlanningDelete = "Permiso:PlanificacionEliminar";

    public const string SettingsRead = "Permiso:ParametrosLeer";
    public const string SettingsManage = "Permiso:ParametrosGestionar";

    public const string IoTRead = "Permiso:IoTLeer";
    public const string DevicesManage = "Permiso:DispositivosGestionar";

    public const string MaintenanceManage = "Permiso:MantenimientoGestionar";

    public const string TerritoryRead = "Permiso:TerritorioLeer";
    public const string TerritoryManage = "Permiso:TerritorioGestionar";

    public const string CatalogsRead = "Permiso:CatalogosLeer";
    public const string CatalogsManage = "Permiso:CatalogosGestionar";
    public const string CatalogsDelete = "Permiso:CatalogosEliminar";

    public const string OperationsRead = "Permiso:OperacionesLeer";

    public const string RolesManage = "Permiso:RolesGestionar";

    public const string SessionsRead = "Permiso:SesionesLeer";

    public const string DashboardRead = "Permiso:SistemaLeer";

    public const string TelemetryRead = "Permiso:TelemetriaLeer";
    public const string TelemetryManage = "Permiso:TelemetriaGestionar";

    public const string WaterSupplyRead = "Permiso:AguaLeer";
    public const string WaterSupplyManage = "Permiso:AguaGestionar";
    public const string WaterSupplyDelete = "Permiso:AguaEliminar";

    public const string AnalyticsRead = "Permiso:AnaliticaLeer";
    public const string AnalyticsManage = "Permiso:AnaliticaGestionar";
    public const string AnalyticsPowerBi = "Permiso:AnaliticaPowerBi";

    public const string AuditRead = "Permiso:AuditoriaLeer";
}
public sealed class JwtOptions
{
    public const string SectionName = "Jwt"; public required string Issuer { get; init; } public required string Audience { get; init; } public required string SigningKey { get; init; }
    public int AccessTokenMinutes { get; init; } = 15; public int RefreshTokenDays { get; init; } = 7; public int PasswordRecoveryMinutes { get; init; } = 30; public int TwoFactorChallengeMinutes { get; init; } = 5;
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