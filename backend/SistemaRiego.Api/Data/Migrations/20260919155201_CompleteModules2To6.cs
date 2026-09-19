using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaRiego.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompleteModules2To6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "IrrigationCorrectionFactor",
                table: "SoilTypes",
                type: "decimal(8,4)",
                precision: 8,
                scale: 4,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<string>(
                name: "BaseUnitCode",
                table: "MasterCatalogItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConversionFactorToBase",
                table: "MasterCatalogItems",
                type: "decimal(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IrrigationZoneId",
                table: "IoTSensors",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReadingFrequencyId",
                table: "IoTSensors",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommunicationProtocol",
                table: "DeviceModels",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Precision",
                table: "DeviceModels",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Voltage",
                table: "DeviceModels",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FarmId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonnelCode",
                table: "AspNetUsers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UniversityCenterId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InventoryMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MovementType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PreviousOwner = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    NewOwner = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PerformedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_IoTDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "IoTDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                columns: new[] { "BaseUnitCode", "ConversionFactorToBase" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                columns: new[] { "BaseUnitCode", "ConversionFactorToBase" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                columns: new[] { "BaseUnitCode", "ConversionFactorToBase" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"),
                columns: new[] { "BaseUnitCode", "ConversionFactorToBase" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"),
                columns: new[] { "BaseUnitCode", "ConversionFactorToBase" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"),
                columns: new[] { "BaseUnitCode", "ConversionFactorToBase" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                columns: new[] { "BaseUnitCode", "ConversionFactorToBase" },
                values: new object[] { "PERCENT", 1m });

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000002"),
                columns: new[] { "BaseUnitCode", "ConversionFactorToBase" },
                values: new object[] { "CELSIUS", 1m });

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000003"),
                columns: new[] { "BaseUnitCode", "ConversionFactorToBase" },
                values: new object[] { "LITER", 1m });

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000001"),
                columns: new[] { "BaseUnitCode", "ConversionFactorToBase" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000002"),
                columns: new[] { "BaseUnitCode", "ConversionFactorToBase" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000003"),
                columns: new[] { "BaseUnitCode", "ConversionFactorToBase" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000004"),
                columns: new[] { "BaseUnitCode", "ConversionFactorToBase" },
                values: new object[] { null, null });

            migrationBuilder.CreateIndex(
                name: "IX_IoTSensors_IrrigationZoneId",
                table: "IoTSensors",
                column: "IrrigationZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_IoTSensors_ReadingFrequencyId",
                table: "IoTSensors",
                column: "ReadingFrequencyId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_FarmId",
                table: "AspNetUsers",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_UniversityCenterId",
                table: "AspNetUsers",
                column: "UniversityCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_DeviceId_OccurredAtUtc",
                table: "InventoryMovements",
                columns: new[] { "DeviceId", "OccurredAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Farms_FarmId",
                table: "AspNetUsers",
                column: "FarmId",
                principalTable: "Farms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_UniversityCenters_UniversityCenterId",
                table: "AspNetUsers",
                column: "UniversityCenterId",
                principalTable: "UniversityCenters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_IoTSensors_IrrigationZones_IrrigationZoneId",
                table: "IoTSensors",
                column: "IrrigationZoneId",
                principalTable: "IrrigationZones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_IoTSensors_MasterCatalogItems_ReadingFrequencyId",
                table: "IoTSensors",
                column: "ReadingFrequencyId",
                principalTable: "MasterCatalogItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Farms_FarmId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_UniversityCenters_UniversityCenterId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_IoTSensors_IrrigationZones_IrrigationZoneId",
                table: "IoTSensors");

            migrationBuilder.DropForeignKey(
                name: "FK_IoTSensors_MasterCatalogItems_ReadingFrequencyId",
                table: "IoTSensors");

            migrationBuilder.DropTable(
                name: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_IoTSensors_IrrigationZoneId",
                table: "IoTSensors");

            migrationBuilder.DropIndex(
                name: "IX_IoTSensors_ReadingFrequencyId",
                table: "IoTSensors");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_FarmId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_UniversityCenterId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IrrigationCorrectionFactor",
                table: "SoilTypes");

            migrationBuilder.DropColumn(
                name: "BaseUnitCode",
                table: "MasterCatalogItems");

            migrationBuilder.DropColumn(
                name: "ConversionFactorToBase",
                table: "MasterCatalogItems");

            migrationBuilder.DropColumn(
                name: "IrrigationZoneId",
                table: "IoTSensors");

            migrationBuilder.DropColumn(
                name: "ReadingFrequencyId",
                table: "IoTSensors");

            migrationBuilder.DropColumn(
                name: "CommunicationProtocol",
                table: "DeviceModels");

            migrationBuilder.DropColumn(
                name: "Precision",
                table: "DeviceModels");

            migrationBuilder.DropColumn(
                name: "Voltage",
                table: "DeviceModels");

            migrationBuilder.DropColumn(
                name: "FarmId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PersonnelCode",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UniversityCenterId",
                table: "AspNetUsers");
        }
    }
}
