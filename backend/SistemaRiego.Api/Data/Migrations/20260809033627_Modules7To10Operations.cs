using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaRiego.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Modules7To10Operations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IrrigationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IrrigationZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CropWaterRequirementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MinimumMoisturePercent = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    TargetMoisturePercent = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    HysteresisPercent = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    MaximumDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    AllowedFrom = table.Column<TimeOnly>(type: "time", nullable: false),
                    AllowedUntil = table.Column<TimeOnly>(type: "time", nullable: false),
                    AllowedDays = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SuspendedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastEvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastDecision = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastReason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IrrigationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IrrigationRules_CropWaterRequirements_CropWaterRequirementId",
                        column: x => x.CropWaterRequirementId,
                        principalTable: "CropWaterRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_IrrigationRules_IrrigationZones_IrrigationZoneId",
                        column: x => x.IrrigationZoneId,
                        principalTable: "IrrigationZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WaterTanks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CapacityLiters = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    CurrentLevelLiters = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    MinimumSafePercent = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    MaximumFillPercent = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastLevelReadingUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterTanks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IrrigationRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IrrigationZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IrrigationRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Mode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlannedDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    FlowRateLitersMinute = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedByEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Observations = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IrrigationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IrrigationRuns_IrrigationRules_IrrigationRuleId",
                        column: x => x.IrrigationRuleId,
                        principalTable: "IrrigationRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_IrrigationRuns_IrrigationZones_IrrigationZoneId",
                        column: x => x.IrrigationZoneId,
                        principalTable: "IrrigationZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WaterPumps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WaterTankId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsRunning = table.Column<bool>(type: "bit", nullable: false),
                    MaximumRunMinutes = table.Column<int>(type: "int", nullable: false),
                    MinimumRestMinutes = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastStoppedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterPumps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaterPumps_WaterTanks_WaterTankId",
                        column: x => x.WaterTankId,
                        principalTable: "WaterTanks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationalEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IrrigationZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IrrigationRunId = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationalEvents_IrrigationRuns_IrrigationRunId",
                        column: x => x.IrrigationRunId,
                        principalTable: "IrrigationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OperationalEvents_IrrigationZones_IrrigationZoneId",
                        column: x => x.IrrigationZoneId,
                        principalTable: "IrrigationZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WaterConsumptionRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IrrigationRunId = table.Column<long>(type: "bigint", nullable: false),
                    IrrigationZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FlowRateLitersMinute = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    DurationMinutes = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    VolumeLiters = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterConsumptionRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaterConsumptionRecords_IrrigationRuns_IrrigationRunId",
                        column: x => x.IrrigationRunId,
                        principalTable: "IrrigationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WaterConsumptionRecords_IrrigationZones_IrrigationZoneId",
                        column: x => x.IrrigationZoneId,
                        principalTable: "IrrigationZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WaterSupplyEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WaterPumpId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InitialLevelLiters = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    FinalLevelLiters = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: true),
                    SuppliedLiters = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterSupplyEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaterSupplyEvents_WaterPumps_WaterPumpId",
                        column: x => x.WaterPumpId,
                        principalTable: "WaterPumps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationRules_CropWaterRequirementId",
                table: "IrrigationRules",
                column: "CropWaterRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationRules_IrrigationZoneId_Name",
                table: "IrrigationRules",
                columns: new[] { "IrrigationZoneId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationRuns_IrrigationRuleId",
                table: "IrrigationRuns",
                column: "IrrigationRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationRuns_IrrigationZoneId_RequestedAtUtc",
                table: "IrrigationRuns",
                columns: new[] { "IrrigationZoneId", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalEvents_IrrigationRunId",
                table: "OperationalEvents",
                column: "IrrigationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalEvents_IrrigationZoneId",
                table: "OperationalEvents",
                column: "IrrigationZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalEvents_OccurredAtUtc",
                table: "OperationalEvents",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_WaterConsumptionRecords_IrrigationRunId",
                table: "WaterConsumptionRecords",
                column: "IrrigationRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaterConsumptionRecords_IrrigationZoneId",
                table: "WaterConsumptionRecords",
                column: "IrrigationZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_WaterConsumptionRecords_RecordedAtUtc",
                table: "WaterConsumptionRecords",
                column: "RecordedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_WaterPumps_WaterTankId",
                table: "WaterPumps",
                column: "WaterTankId");

            migrationBuilder.CreateIndex(
                name: "IX_WaterSupplyEvents_StartedAtUtc",
                table: "WaterSupplyEvents",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_WaterSupplyEvents_WaterPumpId",
                table: "WaterSupplyEvents",
                column: "WaterPumpId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationalEvents");

            migrationBuilder.DropTable(
                name: "WaterConsumptionRecords");

            migrationBuilder.DropTable(
                name: "WaterSupplyEvents");

            migrationBuilder.DropTable(
                name: "IrrigationRuns");

            migrationBuilder.DropTable(
                name: "WaterPumps");

            migrationBuilder.DropTable(
                name: "IrrigationRules");

            migrationBuilder.DropTable(
                name: "WaterTanks");
        }
    }
}
