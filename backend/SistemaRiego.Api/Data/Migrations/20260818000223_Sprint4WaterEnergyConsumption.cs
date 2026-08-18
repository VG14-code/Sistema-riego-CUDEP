using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaRiego.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint4WaterEnergyConsumption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "WaterPumps",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "HasUnacknowledgedFault",
                table: "WaterPumps",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "IoTDeviceId",
                table: "WaterPumps",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastMotorCurrentAmps",
                table: "WaterPumps",
                type: "decimal(8,2)",
                precision: 8,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LastPressureBar",
                table: "WaterPumps",
                type: "decimal(8,2)",
                precision: 8,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastTelemetryAtUtc",
                table: "WaterPumps",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaximumCurrentAmps",
                table: "WaterPumps",
                type: "decimal(8,2)",
                precision: 8,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumPressureBar",
                table: "WaterPumps",
                type: "decimal(8,2)",
                precision: 8,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NominalValveFlowLitersMinute",
                table: "WaterPumps",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RatedFlowLitersMinute",
                table: "WaterPumps",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeviationPercent",
                table: "WaterConsumptionRecords",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedCost",
                table: "WaterConsumptionRecords",
                type: "decimal(12,4)",
                precision: 12,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsMeasured",
                table: "WaterConsumptionRecords",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RecommendedVolumeLiters",
                table: "WaterConsumptionRecords",
                type: "decimal(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresSufficientEnergy",
                table: "IrrigationRules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "FlowReadings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IrrigationZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FlowLitersMinute = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlowReadings_IrrigationZones_IrrigationZoneId",
                        column: x => x.IrrigationZoneId,
                        principalTable: "IrrigationZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PumpStationReadings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WaterTankId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WaterPumpId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LevelLiters = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    PressureBar = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    MotorCurrentAmps = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    IsPumpRunning = table.Column<bool>(type: "bit", nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PumpStationReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PumpStationReadings_WaterPumps_WaterPumpId",
                        column: x => x.WaterPumpId,
                        principalTable: "WaterPumps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PumpStationReadings_WaterTanks_WaterTankId",
                        column: x => x.WaterTankId,
                        principalTable: "WaterTanks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SolarBatteries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CapacityWattHours = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    NominalVoltage = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    MinimumSafeChargePercent = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    CurrentChargePercent = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolarBatteries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SolarPanelArrays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RatedPowerWatts = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    PanelCount = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolarPanelArrays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSafetyStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    EmergencyStopActive = table.Column<bool>(type: "bit", nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClearedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSafetyStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChargeControllers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolarPanelArrayId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolarBatteryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RatedCurrentAmps = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargeControllers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChargeControllers_SolarBatteries_SolarBatteryId",
                        column: x => x.SolarBatteryId,
                        principalTable: "SolarBatteries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChargeControllers_SolarPanelArrays_SolarPanelArrayId",
                        column: x => x.SolarPanelArrayId,
                        principalTable: "SolarPanelArrays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EnergyReadings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChargeControllerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GenerationWatts = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    BatteryPercent = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    ConsumptionWatts = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    BatteryVoltage = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnergyReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnergyReadings_ChargeControllers_ChargeControllerId",
                        column: x => x.ChargeControllerId,
                        principalTable: "ChargeControllers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WaterPumps_Code",
                table: "WaterPumps",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaterPumps_IoTDeviceId",
                table: "WaterPumps",
                column: "IoTDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_ChargeControllers_SolarBatteryId",
                table: "ChargeControllers",
                column: "SolarBatteryId");

            migrationBuilder.CreateIndex(
                name: "IX_ChargeControllers_SolarPanelArrayId",
                table: "ChargeControllers",
                column: "SolarPanelArrayId");

            migrationBuilder.CreateIndex(
                name: "IX_EnergyReadings_CapturedAtUtc",
                table: "EnergyReadings",
                column: "CapturedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EnergyReadings_ChargeControllerId",
                table: "EnergyReadings",
                column: "ChargeControllerId");

            migrationBuilder.CreateIndex(
                name: "IX_EnergyReadings_MessageId",
                table: "EnergyReadings",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlowReadings_IrrigationZoneId_CapturedAtUtc",
                table: "FlowReadings",
                columns: new[] { "IrrigationZoneId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FlowReadings_MessageId",
                table: "FlowReadings",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PumpStationReadings_CapturedAtUtc",
                table: "PumpStationReadings",
                column: "CapturedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PumpStationReadings_MessageId",
                table: "PumpStationReadings",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PumpStationReadings_WaterPumpId",
                table: "PumpStationReadings",
                column: "WaterPumpId");

            migrationBuilder.CreateIndex(
                name: "IX_PumpStationReadings_WaterTankId",
                table: "PumpStationReadings",
                column: "WaterTankId");

            migrationBuilder.AddForeignKey(
                name: "FK_WaterPumps_IoTDevices_IoTDeviceId",
                table: "WaterPumps",
                column: "IoTDeviceId",
                principalTable: "IoTDevices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WaterPumps_IoTDevices_IoTDeviceId",
                table: "WaterPumps");

            migrationBuilder.DropTable(
                name: "EnergyReadings");

            migrationBuilder.DropTable(
                name: "FlowReadings");

            migrationBuilder.DropTable(
                name: "PumpStationReadings");

            migrationBuilder.DropTable(
                name: "SystemSafetyStates");

            migrationBuilder.DropTable(
                name: "ChargeControllers");

            migrationBuilder.DropTable(
                name: "SolarBatteries");

            migrationBuilder.DropTable(
                name: "SolarPanelArrays");

            migrationBuilder.DropIndex(
                name: "IX_WaterPumps_Code",
                table: "WaterPumps");

            migrationBuilder.DropIndex(
                name: "IX_WaterPumps_IoTDeviceId",
                table: "WaterPumps");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "WaterPumps");

            migrationBuilder.DropColumn(
                name: "HasUnacknowledgedFault",
                table: "WaterPumps");

            migrationBuilder.DropColumn(
                name: "IoTDeviceId",
                table: "WaterPumps");

            migrationBuilder.DropColumn(
                name: "LastMotorCurrentAmps",
                table: "WaterPumps");

            migrationBuilder.DropColumn(
                name: "LastPressureBar",
                table: "WaterPumps");

            migrationBuilder.DropColumn(
                name: "LastTelemetryAtUtc",
                table: "WaterPumps");

            migrationBuilder.DropColumn(
                name: "MaximumCurrentAmps",
                table: "WaterPumps");

            migrationBuilder.DropColumn(
                name: "MinimumPressureBar",
                table: "WaterPumps");

            migrationBuilder.DropColumn(
                name: "NominalValveFlowLitersMinute",
                table: "WaterPumps");

            migrationBuilder.DropColumn(
                name: "RatedFlowLitersMinute",
                table: "WaterPumps");

            migrationBuilder.DropColumn(
                name: "DeviationPercent",
                table: "WaterConsumptionRecords");

            migrationBuilder.DropColumn(
                name: "EstimatedCost",
                table: "WaterConsumptionRecords");

            migrationBuilder.DropColumn(
                name: "IsMeasured",
                table: "WaterConsumptionRecords");

            migrationBuilder.DropColumn(
                name: "RecommendedVolumeLiters",
                table: "WaterConsumptionRecords");

            migrationBuilder.DropColumn(
                name: "RequiresSufficientEnergy",
                table: "IrrigationRules");
        }
    }
}
