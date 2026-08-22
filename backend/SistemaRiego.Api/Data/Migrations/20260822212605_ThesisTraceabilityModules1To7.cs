using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaRiego.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ThesisTraceabilityModules1To7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AcquisitionCost",
                table: "IoTDevices",
                type: "decimal(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "IoTDevices",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InventoryStatus",
                table: "IoTDevices",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Owner",
                table: "IoTDevices",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PurchaseDate",
                table: "IoTDevices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "WarrantyUntil",
                table: "IoTDevices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaximumAmbientHumidityPercent",
                table: "CropWaterRequirements",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumAmbientHumidityPercent",
                table: "CropWaterRequirements",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CropRotationPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IrrigationZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CropId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PlannedStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PlannedEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CompatibilityNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropRotationPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CropRotationPlans_CropCycles_PreviousCycleId",
                        column: x => x.PreviousCycleId,
                        principalTable: "CropCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CropRotationPlans_Crops_CropId",
                        column: x => x.CropId,
                        principalTable: "Crops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CropRotationPlans_IrrigationZones_IrrigationZoneId",
                        column: x => x.IrrigationZoneId,
                        principalTable: "IrrigationZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeviceInstallations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IrrigationZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    InstalledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InstalledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InstallerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RemovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceInstallations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceInstallations_IoTDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "IoTDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeviceInstallations_IrrigationZones_IrrigationZoneId",
                        column: x => x.IrrigationZoneId,
                        principalTable: "IrrigationZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FirmwareHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PreviousVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RegisteredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmwareHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FirmwareHistories_IoTNodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "IoTNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RemoteConfigurationCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommandType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    MaximumAttempts = table.Column<int>(type: "int", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteConfigurationCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemoteConfigurationCommands_IoTNodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "IoTNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "MasterCatalogItems",
                columns: new[] { "Id", "Code", "CreatedAtUtc", "Description", "IsActive", "Kind", "Name", "Symbol", "UpdatedAtUtc" },
                values: new object[] { new Guid("10000000-0000-0000-0000-000000000003"), "AIR_HUMIDITY", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sensor de humedad relativa del entorno", true, 1, "Humedad ambiental", null, new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_CropRotationPlans_CropId",
                table: "CropRotationPlans",
                column: "CropId");

            migrationBuilder.CreateIndex(
                name: "IX_CropRotationPlans_IrrigationZoneId_PlannedStartDate",
                table: "CropRotationPlans",
                columns: new[] { "IrrigationZoneId", "PlannedStartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CropRotationPlans_PreviousCycleId",
                table: "CropRotationPlans",
                column: "PreviousCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceInstallations_DeviceId_InstalledAtUtc",
                table: "DeviceInstallations",
                columns: new[] { "DeviceId", "InstalledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceInstallations_IrrigationZoneId",
                table: "DeviceInstallations",
                column: "IrrigationZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_FirmwareHistories_NodeId_RegisteredAtUtc",
                table: "FirmwareHistories",
                columns: new[] { "NodeId", "RegisteredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RemoteConfigurationCommands_NodeId",
                table: "RemoteConfigurationCommands",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteConfigurationCommands_Status_RequestedAtUtc",
                table: "RemoteConfigurationCommands",
                columns: new[] { "Status", "RequestedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CropRotationPlans");

            migrationBuilder.DropTable(
                name: "DeviceInstallations");

            migrationBuilder.DropTable(
                name: "FirmwareHistories");

            migrationBuilder.DropTable(
                name: "RemoteConfigurationCommands");

            migrationBuilder.DeleteData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"));

            migrationBuilder.DropColumn(
                name: "AcquisitionCost",
                table: "IoTDevices");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "IoTDevices");

            migrationBuilder.DropColumn(
                name: "InventoryStatus",
                table: "IoTDevices");

            migrationBuilder.DropColumn(
                name: "Owner",
                table: "IoTDevices");

            migrationBuilder.DropColumn(
                name: "PurchaseDate",
                table: "IoTDevices");

            migrationBuilder.DropColumn(
                name: "WarrantyUntil",
                table: "IoTDevices");

            migrationBuilder.DropColumn(
                name: "MaximumAmbientHumidityPercent",
                table: "CropWaterRequirements");

            migrationBuilder.DropColumn(
                name: "MinimumAmbientHumidityPercent",
                table: "CropWaterRequirements");
        }
    }
}
