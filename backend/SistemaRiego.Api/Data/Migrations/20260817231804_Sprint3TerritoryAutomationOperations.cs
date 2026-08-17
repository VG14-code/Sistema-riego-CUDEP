using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaRiego.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3TerritoryAutomationOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BoundaryGeoJson",
                table: "IrrigationZones",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoundaryGeoJson",
                table: "IrrigationSectors",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "IoTCommands",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "IoTCommands",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "FailedAtUtc",
                table: "IoTCommands",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "IoTCommands",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "IrrigationRunId",
                table: "IoTCommands",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IrrigationZoneId",
                table: "IoTCommands",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IrrigationZoneSensors",
                columns: table => new
                {
                    IrrigationZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SensorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IrrigationZoneSensors", x => new { x.IrrigationZoneId, x.SensorId });
                    table.ForeignKey(
                        name: "FK_IrrigationZoneSensors_IoTSensors_SensorId",
                        column: x => x.SensorId,
                        principalTable: "IoTSensors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IrrigationZoneSensors_IrrigationZones_IrrigationZoneId",
                        column: x => x.IrrigationZoneId,
                        principalTable: "IrrigationZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IrrigationZoneValves",
                columns: table => new
                {
                    IrrigationZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IrrigationZoneValves", x => new { x.IrrigationZoneId, x.DeviceId });
                    table.ForeignKey(
                        name: "FK_IrrigationZoneValves_IoTDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "IoTDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IrrigationZoneValves_IrrigationZones_IrrigationZoneId",
                        column: x => x.IrrigationZoneId,
                        principalTable: "IrrigationZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValveRuntimeStates",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IrrigationZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    State = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    LastCommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastAckAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastReconciledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValveRuntimeStates", x => x.DeviceId);
                    table.ForeignKey(
                        name: "FK_ValveRuntimeStates_IoTDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "IoTDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ValveRuntimeStates_IrrigationZones_IrrigationZoneId",
                        column: x => x.IrrigationZoneId,
                        principalTable: "IrrigationZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IoTCommands_IrrigationRunId",
                table: "IoTCommands",
                column: "IrrigationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_IoTCommands_IrrigationZoneId",
                table: "IoTCommands",
                column: "IrrigationZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_IoTCommands_Status_ExpiresAtUtc",
                table: "IoTCommands",
                columns: new[] { "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationZoneSensors_SensorId",
                table: "IrrigationZoneSensors",
                column: "SensorId");

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationZoneValves_DeviceId",
                table: "IrrigationZoneValves",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_ValveRuntimeStates_IrrigationZoneId",
                table: "ValveRuntimeStates",
                column: "IrrigationZoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_IoTCommands_IrrigationRuns_IrrigationRunId",
                table: "IoTCommands",
                column: "IrrigationRunId",
                principalTable: "IrrigationRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_IoTCommands_IrrigationZones_IrrigationZoneId",
                table: "IoTCommands",
                column: "IrrigationZoneId",
                principalTable: "IrrigationZones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IoTCommands_IrrigationRuns_IrrigationRunId",
                table: "IoTCommands");

            migrationBuilder.DropForeignKey(
                name: "FK_IoTCommands_IrrigationZones_IrrigationZoneId",
                table: "IoTCommands");

            migrationBuilder.DropTable(
                name: "IrrigationZoneSensors");

            migrationBuilder.DropTable(
                name: "IrrigationZoneValves");

            migrationBuilder.DropTable(
                name: "ValveRuntimeStates");

            migrationBuilder.DropIndex(
                name: "IX_IoTCommands_IrrigationRunId",
                table: "IoTCommands");

            migrationBuilder.DropIndex(
                name: "IX_IoTCommands_IrrigationZoneId",
                table: "IoTCommands");

            migrationBuilder.DropIndex(
                name: "IX_IoTCommands_Status_ExpiresAtUtc",
                table: "IoTCommands");

            migrationBuilder.DropColumn(
                name: "BoundaryGeoJson",
                table: "IrrigationZones");

            migrationBuilder.DropColumn(
                name: "BoundaryGeoJson",
                table: "IrrigationSectors");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "IoTCommands");

            migrationBuilder.DropColumn(
                name: "FailedAtUtc",
                table: "IoTCommands");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "IoTCommands");

            migrationBuilder.DropColumn(
                name: "IrrigationRunId",
                table: "IoTCommands");

            migrationBuilder.DropColumn(
                name: "IrrigationZoneId",
                table: "IoTCommands");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "IoTCommands",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
