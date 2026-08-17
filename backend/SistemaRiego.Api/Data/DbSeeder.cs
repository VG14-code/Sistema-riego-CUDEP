using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Models;
namespace SistemaRiego.Api.Data;
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.IsRelational()) await db.Database.MigrateAsync();
        var catalogDefinitions = new (CatalogKind Kind, string Code, string Name, string? Symbol)[]
        {
            (CatalogKind.ValveType, "SOLENOID", "Válvula solenoide", null),
            (CatalogKind.PumpType, "CENTRIFUGAL", "Bomba centrífuga", null),
            (CatalogKind.WaterSource, "TANK", "Tanque de almacenamiento", null),
            (CatalogKind.AlertType, "LOW_MOISTURE", "Humedad baja", null),
            (CatalogKind.SuspensionReason, "MAINTENANCE", "Mantenimiento", null),
            (CatalogKind.ReadingFrequency, "EVERY_MINUTE", "Cada minuto", "60 s")
        };
        foreach (var item in catalogDefinitions)
            if (!await db.MasterCatalogItems.AnyAsync(x => x.Kind == item.Kind && x.Code == item.Code))
                db.MasterCatalogItems.Add(new MasterCatalogItem { Kind = item.Kind, Code = item.Code, Name = item.Name, Symbol = item.Symbol });
        await db.SaveChangesAsync();
        var definitions = new[] { ("usuarios.leer", "Consultar usuarios"), ("usuarios.gestionar", "Gestionar usuarios, roles y estados"), ("riego.operar", "Operar el sistema de riego"), ("dispositivos.gestionar", "Configurar sensores y dispositivos"), ("reportes.leer", "Consultar reportes") };
        foreach (var p in definitions) if (!await db.Permissions.AnyAsync(x => x.Code == p.Item1)) db.Permissions.Add(new Permission { Code = p.Item1, Description = p.Item2 });
        await db.SaveChangesAsync();
        var roleDefinitions = new[]
        {
            (RoleNames.Administrator, "Control total del sistema", definitions.Select(x => x.Item1).ToArray()),
            (RoleNames.Technician, "Configuración técnica y consulta", new[] { "usuarios.leer", "dispositivos.gestionar", "reportes.leer" }),
            (RoleNames.Operator, "Operación cotidiana del riego", new[] { "riego.operar", "reportes.leer" })
        };
        foreach (var d in roleDefinitions)
        {
            var role = await db.Roles.Include(x => x.RolePermissions).SingleOrDefaultAsync(x => x.Name == d.Item1);
            if (role is null) { role = new Role { Name = d.Item1, NormalizedName = d.Item1.ToUpperInvariant(), Description = d.Item2, ConcurrencyStamp = Guid.NewGuid().ToString() }; db.Roles.Add(role); }
            var existing = role.RolePermissions.Select(x => x.PermissionId).ToHashSet();
            foreach (var p in await db.Permissions.Where(x => d.Item3.Contains(x.Code)).ToListAsync()) if (!existing.Contains(p.Id)) role.RolePermissions.Add(new RolePermission { Role = role, Permission = p });
        }
        await db.SaveChangesAsync();
        var email = configuration["SeedAdmin:Email"]?.Trim().ToLowerInvariant(); var password = configuration["SeedAdmin:Password"];
        if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password) && !await db.Users.AnyAsync(x => x.NormalizedEmail == email.ToUpperInvariant()))
        {
            var user = new User { Email = email, UserName = email, NormalizedEmail = email.ToUpperInvariant(), NormalizedUserName = email.ToUpperInvariant(), EmailConfirmed = true, FullName = configuration["SeedAdmin:FullName"] ?? "Administrador", SecurityStamp = Guid.NewGuid().ToString(), ConcurrencyStamp = Guid.NewGuid().ToString() };
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
            user.PasswordHash = hasher.HashPassword(user, password);
            user.Credential = new PasswordCredential { User = user, PasswordHash = user.PasswordHash };
            user.UserRoles.Add(new UserRole { User = user, Role = await db.Roles.SingleAsync(x => x.Name == RoleNames.Administrator) });
            db.Users.Add(user); await db.SaveChangesAsync();
        }
        foreach (var existingUser in await db.Users.Include(x => x.Credential).ToListAsync())
        {
            existingUser.UserName ??= existingUser.Email; existingUser.NormalizedUserName ??= existingUser.NormalizedEmail; existingUser.SecurityStamp ??= Guid.NewGuid().ToString(); existingUser.ConcurrencyStamp ??= Guid.NewGuid().ToString(); existingUser.PasswordHash ??= existingUser.Credential?.PasswordHash;
        }
        foreach (var existingRole in await db.Roles.ToListAsync()) { existingRole.NormalizedName ??= existingRole.Name?.ToUpperInvariant(); existingRole.ConcurrencyStamp ??= Guid.NewGuid().ToString(); }
        await db.SaveChangesAsync();
        if (!await db.IoTNodes.AnyAsync())
        {
            var active = await db.MasterCatalogItems.SingleAsync(x => x.Kind == CatalogKind.OperationalStatus && x.Code == "ACTIVE");
            var raspberryType = await db.MasterCatalogItems.SingleAsync(x => x.Kind == CatalogKind.DeviceType && x.Code == "RASPBERRY_PI");
            var nodeType = await db.MasterCatalogItems.SingleAsync(x => x.Kind == CatalogKind.DeviceType && x.Code == "SENSOR_NODE");
            var moistureType = await db.MasterCatalogItems.SingleAsync(x => x.Kind == CatalogKind.SensorType && x.Code == "SOIL_MOISTURE");
            var temperatureType = await db.MasterCatalogItems.SingleAsync(x => x.Kind == CatalogKind.SensorType && x.Code == "AIR_TEMPERATURE");
            var percent = await db.MasterCatalogItems.SingleAsync(x => x.Kind == CatalogKind.MeasurementUnit && x.Code == "PERCENT");
            var celsius = await db.MasterCatalogItems.SingleAsync(x => x.Kind == CatalogKind.MeasurementUnit && x.Code == "CELSIUS");
            var now = DateTime.UtcNow;
            var gateway = new IoTNode { Code = "RPI-CUDEP-01", Name = "Gateway principal CUDEP", Location = "Granja experimental - caseta técnica", IpAddress = "192.168.1.50", CommunicationProtocol = "HTTP", FirmwareVersion = "1.0.0", OperationalStatusId = active.Id, LastCommunicationUtc = now };
            var raspberry = new IoTDevice { Code = "CTRL-RPI-01", Name = "Controlador Raspberry Pi 4", SerialNumber = "RPI4-CUDEP-001", Manufacturer = "Raspberry Pi", Model = "4 Model B", InstallationLocation = "Caseta técnica", InstallationDate = DateOnly.FromDateTime(now), DeviceTypeId = raspberryType.Id, OperationalStatusId = active.Id, Node = gateway };
            var fieldNodeA = new IoTDevice { Code = "NODO-CAMPO-A", Name = "Nodo sensorial parcela A", SerialNumber = "NODE-A-001", Manufacturer = "CUDEP", Model = "Nodo RK520", InstallationLocation = "Parcela A", InstallationDate = DateOnly.FromDateTime(now), DeviceTypeId = nodeType.Id, OperationalStatusId = active.Id, Node = gateway };
            var fieldNodeB = new IoTDevice { Code = "NODO-CAMPO-B", Name = "Nodo sensorial parcela B", SerialNumber = "NODE-B-001", Manufacturer = "CUDEP", Model = "Nodo RK520", InstallationLocation = "Parcela B", InstallationDate = DateOnly.FromDateTime(now), DeviceTypeId = nodeType.Id, OperationalStatusId = active.Id, Node = gateway };
            var sensorA = new IoTSensor { Code = "HUM-SUELO-A1", Name = "Humedad del suelo A1", SerialNumber = "RK520-A-001-H", Model = "RK520-02", Channel = "RS485-1", MinimumValue = 0, MaximumValue = 100, SensorTypeId = moistureType.Id, MeasurementUnitId = percent.Id, OperationalStatusId = active.Id, Device = fieldNodeA, LastReadingUtc = now };
            var sensorB = new IoTSensor { Code = "HUM-SUELO-B1", Name = "Humedad del suelo B1", SerialNumber = "RK520-B-001-H", Model = "RK520-02", Channel = "RS485-2", MinimumValue = 0, MaximumValue = 100, SensorTypeId = moistureType.Id, MeasurementUnitId = percent.Id, OperationalStatusId = active.Id, Device = fieldNodeB, LastReadingUtc = now };
            var sensorTemp = new IoTSensor { Code = "TEMP-SUELO-A1", Name = "Temperatura del suelo A1", SerialNumber = "RK520-A-001-T", Model = "RK520-02", Channel = "RS485-1", MinimumValue = -40, MaximumValue = 80, SensorTypeId = temperatureType.Id, MeasurementUnitId = celsius.Id, OperationalStatusId = active.Id, Device = fieldNodeA, LastReadingUtc = now };
            db.IoTNodes.Add(gateway);
            db.IoTDevices.AddRange(raspberry, fieldNodeA, fieldNodeB);
            db.IoTSensors.AddRange(sensorA, sensorB, sensorTemp);
            db.SensorCalibrations.AddRange(
                new SensorCalibration { Sensor = sensorA, CalibratedAtUtc = now, ReferenceValue = 50, MeasuredValue = 48.7m, AppliedOffset = 1.3m, Notes = "Calibración inicial en condición controlada." },
                new SensorCalibration { Sensor = sensorB, CalibratedAtUtc = now, ReferenceValue = 50, MeasuredValue = 51.1m, AppliedOffset = -1.1m, Notes = "Calibración inicial en condición controlada." });
            await db.SaveChangesAsync();
        }
        if (!await db.UniversityCenters.AnyAsync())
        {
            var active = await db.MasterCatalogItems.SingleAsync(x => x.Kind == CatalogKind.OperationalStatus && x.Code == "ACTIVE");
            var sensor = await db.IoTSensors.SingleAsync(x => x.Code == "HUM-SUELO-A1");
            var center = new UniversityCenter { Code = "CUDEP", Name = "Centro Universitario de Petén", Location = "Santa Elena, Petén", Contact = "Área de agricultura inteligente" };
            var soil = new SoilType { Code = "FRANCO", Name = "Suelo franco", FieldCapacityPercent = 32, SaturationPercent = 48, InfiltrationMillimetersHour = 18, Description = "Equilibrio de arena, limo y arcilla; adecuado para el cultivo demostrativo." };
            var farm = new Farm { UniversityCenter = center, Code = "GRANJA-CUDEP", Name = "Granja experimental CUDEP", Location = "Campus CUDEP", Latitude = 16.9264m, Longitude = -89.8914m };
            var block = new FarmBlock { Farm = farm, SoilType = soil, Code = "BLOQUE-A", Name = "Bloque productivo A", AreaHectares = 1.25m, Description = "Área piloto instrumentada." };
            var sector = new IrrigationSector { FarmBlock = block, Code = "SECTOR-A", Name = "Sector de riego norte", AreaHectares = .65m, SlopePercent = 2.5m };
            var zone = new IrrigationZone { IrrigationSector = sector, Code = "ZONA-A1", Name = "Zona tomate A1", AreaHectares = .32m, OperationalStatusId = active.Id, PrimarySensorId = sensor.Id, Latitude = 16.9265m, Longitude = -89.8912m };
            var cropType = new CropType { Code = "HORTALIZA", Name = "Hortaliza" };
            var crop = new Crop { CropType = cropType, Code = "TOMATE", Name = "Tomate", ScientificName = "Solanum lycopersicum", Description = "Cultivo piloto para recomendaciones de riego." };
            var germination = new PhenologicalStage { Crop = crop, Name = "Germinación", Sequence = 1, EstimatedDays = 12, Description = "Emergencia y establecimiento inicial." };
            var growth = new PhenologicalStage { Crop = crop, Name = "Crecimiento vegetativo", Sequence = 2, EstimatedDays = 30, Description = "Desarrollo de follaje y raíces." };
            var flowering = new PhenologicalStage { Crop = crop, Name = "Floración y fructificación", Sequence = 3, EstimatedDays = 45, Description = "Mayor demanda hídrica." };
            var maturity = new PhenologicalStage { Crop = crop, Name = "Maduración", Sequence = 4, EstimatedDays = 30, Description = "Control de humedad previo a cosecha." };
            db.AddRange(center, soil, farm, block, sector, zone, cropType, crop, germination, growth, flowering, maturity);
            await db.SaveChangesAsync();
            db.CropWaterRequirements.AddRange(
                new CropWaterRequirement { CropId = crop.Id, SoilTypeId = soil.Id, MinimumMoisturePercent = 42, TargetMoisturePercent = 58, MaximumMoisturePercent = 74, BaseVolumeLiters = 950, FrequencyHours = 24, BaseDurationMinutes = 18, AllowedFrom = new TimeOnly(5, 0), AllowedUntil = new TimeOnly(9, 0) },
                new CropWaterRequirement { CropId = crop.Id, PhenologicalStageId = flowering.Id, SoilTypeId = soil.Id, MinimumMoisturePercent = 48, TargetMoisturePercent = 64, MaximumMoisturePercent = 78, BaseVolumeLiters = 1250, FrequencyHours = 18, BaseDurationMinutes = 24, AllowedFrom = new TimeOnly(5, 0), AllowedUntil = new TimeOnly(8, 30) });
            db.CropCycles.Add(new CropCycle { CropId = crop.Id, IrrigationZoneId = zone.Id, CurrentStageId = growth.Id, Name = "Tomate parcela A · ciclo 2026", SowingDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-24)), ExpectedHarvestDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(92)), AreaHectares = .32m, PlantCount = 1800, Status = "Activo", Notes = "Ciclo demostrativo del avance de tesis." });
            var start = DateTime.UtcNow.AddHours(-22);
            for (var i = 0; i < 12; i++) db.SensorReadings.Add(new SensorReading { SensorId = sensor.Id, IrrigationZoneId = zone.Id, CapturedAtUtc = start.AddHours(i * 2), Value = 56m - i * 1.25m + (i % 3), BatteryPercent = 94 - i * .3m, SignalStrength = -58 - i % 4, MessageId = $"SEED-HUM-A1-{i:00}", Transport = "SIMULACIÓN", IsValid = true, ValidationStatus = "Válida" });
            var temp = await db.IoTSensors.SingleAsync(x => x.Code == "TEMP-SUELO-A1");
            for (var i = 0; i < 12; i++) db.SensorReadings.Add(new SensorReading { SensorId = temp.Id, IrrigationZoneId = zone.Id, CapturedAtUtc = start.AddHours(i * 2), Value = 23m + (i % 5) * .6m, BatteryPercent = 91 - i * .2m, SignalStrength = -61, MessageId = $"SEED-TEMP-A1-{i:00}", Transport = "SIMULACIÓN", IsValid = true, ValidationStatus = "Válida" });
            db.AccessAudits.Add(new AccessAudit { EventType = "PLAN_MODULES_SEEDED", Detail = "Módulos 1 al 7 preparados con estructura territorial, agronomía, ciclo y telemetría." });
            await db.SaveChangesAsync();
        }
        await Sprint3Seeder.SeedAsync(db);
        await Modules7To10Seeder.SeedAsync(db);
        await EmpiricalDashboardSeeder.SeedAsync(db);
    }
}
