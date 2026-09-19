using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Tests;

/// Verifica que cada controller migrado de RequireRole a permisos finos (fase de
/// "migrar el resto de controllers") solo use politicas del dominio esperado, sin
/// que haya quedado una accion apuntando a Policies.X (residual) o a la politica
/// de otro dominio por error de sustitucion.
public sealed class PermissionMigrationAuthorizationTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return Case<AgronomyController>(PermissionPolicies.AgronomyRead, PermissionPolicies.AgronomyManage, PermissionPolicies.AgronomyDelete, PermissionPolicies.IrrigationOperate);
        yield return Case<AlertsController>(PermissionPolicies.AlertsRead, PermissionPolicies.AlertsManage);
        yield return Case<AuditController>(PermissionPolicies.AuditRead);
        yield return Case<AuditTrailController>(PermissionPolicies.AuditRead);
        yield return Case<AutomationController>(PermissionPolicies.AutomationRead, PermissionPolicies.AutomationManage);
        yield return Case<CropPlanningController>(PermissionPolicies.CropPlanningRead, PermissionPolicies.CropPlanningManage, PermissionPolicies.CropPlanningDelete);
        yield return Case<CropRotationsController>(PermissionPolicies.CropPlanningRead, PermissionPolicies.CropPlanningManage, PermissionPolicies.CropPlanningDelete);
        yield return Case<DeviceCatalogsController>(PermissionPolicies.DeviceCatalogsRead, PermissionPolicies.DeviceCatalogsManage, PermissionPolicies.DeviceCatalogsDelete);
        yield return Case<EnergyController>(PermissionPolicies.EnergyRead);
        yield return Case<GlobalParametersController>(PermissionPolicies.SettingsRead, PermissionPolicies.SettingsManage);
        yield return Case<IoTController>(PermissionPolicies.IoTRead, PermissionPolicies.DevicesManage);
        yield return Case<IoTCommunicationController>(PermissionPolicies.IoTRead);
        yield return Case<IoTTraceabilityController>(PermissionPolicies.IoTRead, PermissionPolicies.DevicesManage);
        yield return Case<MaintenanceController>(PermissionPolicies.MaintenanceManage);
        yield return Case<ManualIrrigationController>(PermissionPolicies.IrrigationOperate);
        yield return Case<MasterDataController>(PermissionPolicies.CatalogsRead, PermissionPolicies.CatalogsManage, PermissionPolicies.CatalogsDelete);
        yield return Case<OperationsController>(PermissionPolicies.OperationsRead);
        yield return Case<ReportsController>(PermissionPolicies.ReportsRead);
        yield return Case<RolesController>(PermissionPolicies.RolesManage);
        yield return Case<SessionsController>(PermissionPolicies.SessionsRead);
        yield return Case<SystemDashboardController>(PermissionPolicies.DashboardRead);
        yield return Case<TelemetryController>(PermissionPolicies.TelemetryRead, PermissionPolicies.TelemetryManage);
        yield return Case<TerritoryController>(PermissionPolicies.TerritoryRead, PermissionPolicies.TerritoryManage);
        yield return Case<TerritoryMaintenanceController>(PermissionPolicies.TerritoryManage);
        yield return Case<UsersController>(PermissionPolicies.UsersManage);
        yield return Case<WaterSupplyController>(PermissionPolicies.WaterSupplyRead, PermissionPolicies.WaterSupplyManage, PermissionPolicies.WaterSupplyDelete);
        yield return Case<Week14AnalyticsController>(PermissionPolicies.AnalyticsRead, PermissionPolicies.AnalyticsPowerBi);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Controller_ActionsOnlyUseExpectedPermissionPolicies(Type controllerType, HashSet<string> expected)
    {
        var actual = EffectivePolicies(controllerType);
        Assert.NotEmpty(actual);
        Assert.True(expected.SetEquals(actual), $"{controllerType.Name}: se esperaba {{{string.Join(", ", expected)}}} pero se encontro {{{string.Join(", ", actual)}}}.");
    }

    [Fact]
    public void AuthController_Register_RequiresUsersManage()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.Register))!;
        var attribute = method.GetCustomAttributes<AuthorizeAttribute>().Single();
        Assert.Equal(PermissionPolicies.UsersManage, attribute.Policy);
    }

    [Fact]
    public void TelemetryHub_RequiresTelemetryRead()
    {
        var attribute = typeof(Hubs.TelemetryHub).GetCustomAttributes<AuthorizeAttribute>().Single();
        Assert.Equal(PermissionPolicies.TelemetryRead, attribute.Policy);
    }

    private static object[] Case<T>(params string[] expectedPolicies) where T : ControllerBase => [typeof(T), expectedPolicies.ToHashSet()];

    private static HashSet<string> EffectivePolicies(Type controllerType)
    {
        var classPolicy = controllerType.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).FirstOrDefault();
        var actionMethods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name.StartsWith("Http", StringComparison.Ordinal)));
        var policies = new HashSet<string>();
        foreach (var method in actionMethods)
        {
            var methodPolicy = method.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).FirstOrDefault();
            var effective = methodPolicy ?? classPolicy ?? throw new InvalidOperationException($"{controllerType.Name}.{method.Name} no tiene politica de autorizacion.");
            policies.Add(effective);
        }
        return policies;
    }
}
