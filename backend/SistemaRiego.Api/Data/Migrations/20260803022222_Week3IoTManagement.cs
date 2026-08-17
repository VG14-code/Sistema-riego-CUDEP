using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaRiego.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Week3IoTManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IoTNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    MacAddress = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CommunicationProtocol = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FirmwareVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OperationalStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastCommunicationUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IoTNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IoTNodes_MasterCatalogItems_OperationalStatusId",
                        column: x => x.OperationalStatusId,
                        principalTable: "MasterCatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IoTDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Manufacturer = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InstallationLocation = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    InstallationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DeviceTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationalStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IoTDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IoTDevices_IoTNodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "IoTNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_IoTDevices_MasterCatalogItems_DeviceTypeId",
                        column: x => x.DeviceTypeId,
                        principalTable: "MasterCatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IoTDevices_MasterCatalogItems_OperationalStatusId",
                        column: x => x.OperationalStatusId,
                        principalTable: "MasterCatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IoTSensors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    MinimumValue = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    MaximumValue = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    CalibrationOffset = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    SensorTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeasurementUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationalStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastReadingUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IoTSensors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IoTSensors_IoTDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "IoTDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_IoTSensors_MasterCatalogItems_MeasurementUnitId",
                        column: x => x.MeasurementUnitId,
                        principalTable: "MasterCatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IoTSensors_MasterCatalogItems_OperationalStatusId",
                        column: x => x.OperationalStatusId,
                        principalTable: "MasterCatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IoTSensors_MasterCatalogItems_SensorTypeId",
                        column: x => x.SensorTypeId,
                        principalTable: "MasterCatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SensorCalibrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SensorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalibratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReferenceValue = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    MeasuredValue = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    AppliedOffset = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CalibratedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SensorCalibrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SensorCalibrations_IoTSensors_SensorId",
                        column: x => x.SensorId,
                        principalTable: "IoTSensors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IoTDevices_Code",
                table: "IoTDevices",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IoTDevices_DeviceTypeId",
                table: "IoTDevices",
                column: "DeviceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_IoTDevices_NodeId",
                table: "IoTDevices",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_IoTDevices_OperationalStatusId",
                table: "IoTDevices",
                column: "OperationalStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_IoTDevices_SerialNumber",
                table: "IoTDevices",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IoTNodes_Code",
                table: "IoTNodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IoTNodes_OperationalStatusId",
                table: "IoTNodes",
                column: "OperationalStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_IoTSensors_Code",
                table: "IoTSensors",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IoTSensors_DeviceId",
                table: "IoTSensors",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_IoTSensors_MeasurementUnitId",
                table: "IoTSensors",
                column: "MeasurementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_IoTSensors_OperationalStatusId",
                table: "IoTSensors",
                column: "OperationalStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_IoTSensors_SensorTypeId",
                table: "IoTSensors",
                column: "SensorTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_IoTSensors_SerialNumber",
                table: "IoTSensors",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SensorCalibrations_SensorId_CalibratedAtUtc",
                table: "SensorCalibrations",
                columns: new[] { "SensorId", "CalibratedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SensorCalibrations");

            migrationBuilder.DropTable(
                name: "IoTSensors");

            migrationBuilder.DropTable(
                name: "IoTDevices");

            migrationBuilder.DropTable(
                name: "IoTNodes");
        }
    }
}
