namespace SistemaRiego.Api.Models;

public static class PermissionCodes
{
    public const string UsersRead = "usuarios.leer";
    public const string UsersManage = "usuarios.gestionar";
    public const string IrrigationOperate = "riego.operar";
    public const string DevicesManage = "dispositivos.gestionar";
    public const string ReportsRead = "reportes.leer";
    public const string DeviceCatalogsRead = "dispositivos.catalogos.leer";
    public const string DeviceCatalogsManage = "dispositivos.catalogos.gestionar";
    public const string DeviceCatalogsDelete = "dispositivos.catalogos.eliminar";

    public const string AgronomyRead = "agronomia.leer";
    public const string AgronomyManage = "agronomia.gestionar";
    public const string AgronomyDelete = "agronomia.eliminar";

    public const string AlertsRead = "alertas.leer";
    public const string AlertsManage = "alertas.gestionar";

    public const string AutomationRead = "automatizacion.leer";
    public const string AutomationManage = "automatizacion.gestionar";

    public const string CropPlanningRead = "planificacion.leer";
    public const string CropPlanningManage = "planificacion.gestionar";
    public const string CropPlanningDelete = "planificacion.eliminar";

    public const string SettingsRead = "parametros.leer";
    public const string SettingsManage = "parametros.gestionar";

    public const string IoTRead = "iot.leer";

    public const string MaintenanceManage = "mantenimiento.gestionar";

    public const string TerritoryRead = "territorio.leer";
    public const string TerritoryManage = "territorio.gestionar";

    public const string CatalogsRead = "catalogos.leer";
    public const string CatalogsManage = "catalogos.gestionar";
    public const string CatalogsDelete = "catalogos.eliminar";

    public const string OperationsRead = "operaciones.leer";

    public const string RolesManage = "roles.gestionar";

    public const string SessionsRead = "sesiones.leer";

    public const string DashboardRead = "sistema.leer";

    public const string TelemetryRead = "telemetria.leer";
    public const string TelemetryManage = "telemetria.gestionar";

    public const string WaterSupplyRead = "agua.leer";
    public const string WaterSupplyManage = "agua.gestionar";
    public const string WaterSupplyDelete = "agua.eliminar";

    public const string AnalyticsRead = "analitica.leer";
    public const string AnalyticsPowerBi = "analitica.powerbi";

    public const string AuditRead = "auditoria.leer";
}
