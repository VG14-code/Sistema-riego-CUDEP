using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaRiego.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Modules1To7Expansion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CropTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IoTCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommandType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IoTCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IoTCommands_IoTDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "IoTDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SoilTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FieldCapacityPercent = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    SaturationPercent = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    InfiltrationMillimetersHour = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoilTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UniversityCenters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Contact = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UniversityCenters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Crops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CropTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScientificName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Crops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Crops_CropTypes_CropTypeId",
                        column: x => x.CropTypeId,
                        principalTable: "CropTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Farms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniversityCenterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Farms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Farms_UniversityCenters_UniversityCenterId",
                        column: x => x.UniversityCenterId,
                        principalTable: "UniversityCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhenologicalStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CropId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    EstimatedDays = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhenologicalStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhenologicalStages_Crops_CropId",
                        column: x => x.CropId,
                        principalTable: "Crops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FarmBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SoilTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AreaHectares = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FarmBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FarmBlocks_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FarmBlocks_SoilTypes_SoilTypeId",
                        column: x => x.SoilTypeId,
                        principalTable: "SoilTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CropWaterRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CropId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhenologicalStageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SoilTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MinimumMoisturePercent = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    TargetMoisturePercent = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    MaximumMoisturePercent = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    BaseVolumeLiters = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    FrequencyHours = table.Column<int>(type: "int", nullable: false),
                    BaseDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    MinimumTemperatureCelsius = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    MaximumTemperatureCelsius = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    AllowedFrom = table.Column<TimeOnly>(type: "time", nullable: true),
                    AllowedUntil = table.Column<TimeOnly>(type: "time", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropWaterRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CropWaterRequirements_Crops_CropId",
                        column: x => x.CropId,
                        principalTable: "Crops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CropWaterRequirements_PhenologicalStages_PhenologicalStageId",
                        column: x => x.PhenologicalStageId,
                        principalTable: "PhenologicalStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CropWaterRequirements_SoilTypes_SoilTypeId",
                        column: x => x.SoilTypeId,
                        principalTable: "SoilTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "IrrigationSectors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmBlockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AreaHectares = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    SlopePercent = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IrrigationSectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IrrigationSectors_FarmBlocks_FarmBlockId",
                        column: x => x.FarmBlockId,
                        principalTable: "FarmBlocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IrrigationZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IrrigationSectorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AreaHectares = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    OperationalStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrimarySensorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValveDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IrrigationZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IrrigationZones_IoTDevices_ValveDeviceId",
                        column: x => x.ValveDeviceId,
                        principalTable: "IoTDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_IrrigationZones_IoTSensors_PrimarySensorId",
                        column: x => x.PrimarySensorId,
                        principalTable: "IoTSensors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_IrrigationZones_IrrigationSectors_IrrigationSectorId",
                        column: x => x.IrrigationSectorId,
                        principalTable: "IrrigationSectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IrrigationZones_MasterCatalogItems_OperationalStatusId",
                        column: x => x.OperationalStatusId,
                        principalTable: "MasterCatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CropCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CropId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IrrigationZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentStageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SowingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedHarvestDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ActualHarvestDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AreaHectares = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    PlantCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropCycles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CropCycles_Crops_CropId",
                        column: x => x.CropId,
                        principalTable: "Crops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CropCycles_IrrigationZones_IrrigationZoneId",
                        column: x => x.IrrigationZoneId,
                        principalTable: "IrrigationZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CropCycles_PhenologicalStages_CurrentStageId",
                        column: x => x.CurrentStageId,
                        principalTable: "PhenologicalStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SensorReadings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SensorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IrrigationZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    BatteryPercent = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    SignalStrength = table.Column<int>(type: "int", nullable: true),
                    MessageId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsValid = table.Column<bool>(type: "bit", nullable: false),
                    ValidationStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Transport = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SensorReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SensorReadings_IoTSensors_SensorId",
                        column: x => x.SensorId,
                        principalTable: "IoTSensors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SensorReadings_IrrigationZones_IrrigationZoneId",
                        column: x => x.IrrigationZoneId,
                        principalTable: "IrrigationZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CropCycles_CropId",
                table: "CropCycles",
                column: "CropId");

            migrationBuilder.CreateIndex(
                name: "IX_CropCycles_CurrentStageId",
                table: "CropCycles",
                column: "CurrentStageId");

            migrationBuilder.CreateIndex(
                name: "IX_CropCycles_IrrigationZoneId",
                table: "CropCycles",
                column: "IrrigationZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_Crops_Code",
                table: "Crops",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Crops_CropTypeId",
                table: "Crops",
                column: "CropTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CropTypes_Code",
                table: "CropTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CropWaterRequirements_CropId",
                table: "CropWaterRequirements",
                column: "CropId");

            migrationBuilder.CreateIndex(
                name: "IX_CropWaterRequirements_PhenologicalStageId",
                table: "CropWaterRequirements",
                column: "PhenologicalStageId");

            migrationBuilder.CreateIndex(
                name: "IX_CropWaterRequirements_SoilTypeId",
                table: "CropWaterRequirements",
                column: "SoilTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_FarmBlocks_Code",
                table: "FarmBlocks",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FarmBlocks_FarmId",
                table: "FarmBlocks",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_FarmBlocks_SoilTypeId",
                table: "FarmBlocks",
                column: "SoilTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Farms_Code",
                table: "Farms",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Farms_UniversityCenterId",
                table: "Farms",
                column: "UniversityCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_IoTCommands_DeviceId",
                table: "IoTCommands",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationSectors_Code",
                table: "IrrigationSectors",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationSectors_FarmBlockId",
                table: "IrrigationSectors",
                column: "FarmBlockId");

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationZones_Code",
                table: "IrrigationZones",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationZones_IrrigationSectorId",
                table: "IrrigationZones",
                column: "IrrigationSectorId");

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationZones_OperationalStatusId",
                table: "IrrigationZones",
                column: "OperationalStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationZones_PrimarySensorId",
                table: "IrrigationZones",
                column: "PrimarySensorId");

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationZones_ValveDeviceId",
                table: "IrrigationZones",
                column: "ValveDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_PhenologicalStages_CropId_Sequence",
                table: "PhenologicalStages",
                columns: new[] { "CropId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SensorReadings_IrrigationZoneId",
                table: "SensorReadings",
                column: "IrrigationZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_SensorReadings_MessageId",
                table: "SensorReadings",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SensorReadings_SensorId_CapturedAtUtc",
                table: "SensorReadings",
                columns: new[] { "SensorId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SoilTypes_Code",
                table: "SoilTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UniversityCenters_Code",
                table: "UniversityCenters",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CropCycles");

            migrationBuilder.DropTable(
                name: "CropWaterRequirements");

            migrationBuilder.DropTable(
                name: "IoTCommands");

            migrationBuilder.DropTable(
                name: "SensorReadings");

            migrationBuilder.DropTable(
                name: "PhenologicalStages");

            migrationBuilder.DropTable(
                name: "IrrigationZones");

            migrationBuilder.DropTable(
                name: "Crops");

            migrationBuilder.DropTable(
                name: "IrrigationSectors");

            migrationBuilder.DropTable(
                name: "CropTypes");

            migrationBuilder.DropTable(
                name: "FarmBlocks");

            migrationBuilder.DropTable(
                name: "Farms");

            migrationBuilder.DropTable(
                name: "SoilTypes");

            migrationBuilder.DropTable(
                name: "UniversityCenters");
        }
    }
}
