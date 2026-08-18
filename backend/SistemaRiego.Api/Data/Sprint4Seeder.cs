using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Data;

public static class Sprint4Seeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        var pump = await db.WaterPumps.FirstOrDefaultAsync();
        if (pump is not null)
        {
            pump.Code = "BOMBA-ABAST-01"; pump.RatedFlowLitersMinute = 36; pump.NominalValveFlowLitersMinute = 12; pump.MinimumPressureBar = 1.2m; pump.MaximumCurrentAmps = 12;
            if (pump.IoTDeviceId is null)
            {
                var device = await db.IoTDevices.SingleOrDefaultAsync(x => x.Code == "BOMBA-ABAST-01");
                if (device is null)
                {
                    var type = await db.MasterCatalogItems.FirstAsync(x => x.Kind == CatalogKind.DeviceType && x.IsActive);
                    var status = await db.MasterCatalogItems.SingleAsync(x => x.Kind == CatalogKind.OperationalStatus && x.Code == "ACTIVE");
                    device = new IoTDevice { Code = "BOMBA-ABAST-01", Name = "Control bomba abastecimiento", SerialNumber = "PUMP-SIM-001", Manufacturer = "Simulada", Model = "MQTT-PUMP", InstallationLocation = "Caseta técnica", DeviceTypeId = type.Id, OperationalStatusId = status.Id };
                    db.IoTDevices.Add(device);
                }
                pump.IoTDevice = device;
            }
        }
        if (!await db.SystemSafetyStates.AnyAsync()) db.SystemSafetyStates.Add(new SystemSafetyState());
        if (!await db.ChargeControllers.AnyAsync())
        {
            var array = new SolarPanelArray { Name = "Arreglo solar CUDEP", PanelCount = 4, RatedPowerWatts = 1800 };
            var battery = new SolarBattery { Name = "Banco LiFePO4", CapacityWattHours = 5120, NominalVoltage = 48, MinimumSafeChargePercent = 20, CurrentChargePercent = 82 };
            db.ChargeControllers.Add(new ChargeController { Name = "Controlador MPPT principal", SolarPanelArray = array, SolarBattery = battery, RatedCurrentAmps = 60 });
        }
        await Upsert(db, "WATER_TARIFF_PER_M3", "3.50", "Tarifa demostrativa GTQ por metro cúbico; costo = litros / 1000 × tarifa.");
        await Upsert(db, "MIN_AUTOMATION_BATTERY_PERCENT", "25", "Carga mínima para permitir automatización de riego.");
        await db.SaveChangesAsync();
    }
    private static async Task Upsert(AppDbContext db, string key, string value, string description)
    { if (!await db.GlobalParameters.AnyAsync(x => x.Key == key)) db.GlobalParameters.Add(new GlobalParameter { Key = key, Value = value, DataType = "decimal", Category = "Sprint 4", Description = description }); }
}
