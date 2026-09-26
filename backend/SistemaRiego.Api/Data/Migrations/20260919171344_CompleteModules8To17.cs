using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaRiego.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompleteModules8To17 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationExecutions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Integration = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HttpStatusCode = table.Column<int>(type: "int", nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExecutedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationExecutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IrrigationRuleEvaluations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IrrigationRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsSimulation = table.Column<bool>(type: "bit", nullable: false),
                    MoisturePercent = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    Decision = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartedIrrigation = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IrrigationRuleEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IrrigationRuleEvaluations_IrrigationRules_IrrigationRuleId",
                        column: x => x.IrrigationRuleId,
                        principalTable: "IrrigationRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IrrigationRuleVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IrrigationRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangedByEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IrrigationRuleVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IrrigationRuleVersions_IrrigationRules_IrrigationRuleId",
                        column: x => x.IrrigationRuleId,
                        principalTable: "IrrigationRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IrrigationSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IrrigationZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NextRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Recurrence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntervalDays = table.Column<int>(type: "int", nullable: true),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    FlowRateLitersMinute = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastResult = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByEmail = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IrrigationSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IrrigationSchedules_IrrigationZones_IrrigationZoneId",
                        column: x => x.IrrigationZoneId,
                        principalTable: "IrrigationZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceWorkOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EquipmentType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EquipmentId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TechnicianName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechnicianEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommitmentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceWorkOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AlertType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MinimumSeverity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Channels = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Recipients = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DailySummary = table.Column<bool>(type: "bit", nullable: false),
                    WeeklySummary = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WaterEfficiencyBaselines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IrrigationZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LitersPerEvent = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    AnomalyThresholdPercent = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    ValidFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterEfficiencyBaselines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaterEfficiencyBaselines_IrrigationZones_IrrigationZoneId",
                        column: x => x.IrrigationZoneId,
                        principalTable: "IrrigationZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WaterSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaximumFlowLitersMinute = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutomaticFillConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WaterTankId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WaterPumpId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WaterSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartAtPercent = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    StopAtPercent = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LastEvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastDecision = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomaticFillConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomaticFillConfigurations_WaterPumps_WaterPumpId",
                        column: x => x.WaterPumpId,
                        principalTable: "WaterPumps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomaticFillConfigurations_WaterSources_WaterSourceId",
                        column: x => x.WaterSourceId,
                        principalTable: "WaterSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomaticFillConfigurations_WaterTanks_WaterTankId",
                        column: x => x.WaterTankId,
                        principalTable: "WaterTanks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HydraulicConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IrrigationZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WaterSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WaterTankId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WaterPumpId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PipeDiameterMillimeters = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    PipeLengthMeters = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    DesignFlowLitersMinute = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    MinimumPressureBar = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    MaximumPressureBar = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HydraulicConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HydraulicConfigurations_IrrigationZones_IrrigationZoneId",
                        column: x => x.IrrigationZoneId,
                        principalTable: "IrrigationZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HydraulicConfigurations_WaterPumps_WaterPumpId",
                        column: x => x.WaterPumpId,
                        principalTable: "WaterPumps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HydraulicConfigurations_WaterSources_WaterSourceId",
                        column: x => x.WaterSourceId,
                        principalTable: "WaterSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HydraulicConfigurations_WaterTanks_WaterTankId",
                        column: x => x.WaterTankId,
                        principalTable: "WaterTanks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutomaticFillConfigurations_WaterPumpId",
                table: "AutomaticFillConfigurations",
                column: "WaterPumpId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomaticFillConfigurations_WaterSourceId",
                table: "AutomaticFillConfigurations",
                column: "WaterSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomaticFillConfigurations_WaterTankId",
                table: "AutomaticFillConfigurations",
                column: "WaterTankId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HydraulicConfigurations_IrrigationZoneId",
                table: "HydraulicConfigurations",
                column: "IrrigationZoneId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HydraulicConfigurations_WaterPumpId",
                table: "HydraulicConfigurations",
                column: "WaterPumpId");

            migrationBuilder.CreateIndex(
                name: "IX_HydraulicConfigurations_WaterSourceId",
                table: "HydraulicConfigurations",
                column: "WaterSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_HydraulicConfigurations_WaterTankId",
                table: "HydraulicConfigurations",
                column: "WaterTankId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationExecutions_Integration_ExecutedAtUtc",
                table: "IntegrationExecutions",
                columns: new[] { "Integration", "ExecutedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationRuleEvaluations_IrrigationRuleId_EvaluatedAtUtc",
                table: "IrrigationRuleEvaluations",
                columns: new[] { "IrrigationRuleId", "EvaluatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationRuleVersions_IrrigationRuleId_Version",
                table: "IrrigationRuleVersions",
                columns: new[] { "IrrigationRuleId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationSchedules_IrrigationZoneId",
                table: "IrrigationSchedules",
                column: "IrrigationZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationSchedules_IsActive_NextRunAtUtc",
                table: "IrrigationSchedules",
                columns: new[] { "IsActive", "NextRunAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWorkOrders_EquipmentType_EquipmentId",
                table: "MaintenanceWorkOrders",
                columns: new[] { "EquipmentType", "EquipmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWorkOrders_Number",
                table: "MaintenanceWorkOrders",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRules_Name",
                table: "NotificationRules",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaterEfficiencyBaselines_IrrigationZoneId",
                table: "WaterEfficiencyBaselines",
                column: "IrrigationZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_WaterSources_Code",
                table: "WaterSources",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomaticFillConfigurations");

            migrationBuilder.DropTable(
                name: "HydraulicConfigurations");

            migrationBuilder.DropTable(
                name: "IntegrationExecutions");

            migrationBuilder.DropTable(
                name: "IrrigationRuleEvaluations");

            migrationBuilder.DropTable(
                name: "IrrigationRuleVersions");

            migrationBuilder.DropTable(
                name: "IrrigationSchedules");

            migrationBuilder.DropTable(
                name: "MaintenanceWorkOrders");

            migrationBuilder.DropTable(
                name: "NotificationRules");

            migrationBuilder.DropTable(
                name: "WaterEfficiencyBaselines");

            migrationBuilder.DropTable(
                name: "WaterSources");
        }
    }
}
