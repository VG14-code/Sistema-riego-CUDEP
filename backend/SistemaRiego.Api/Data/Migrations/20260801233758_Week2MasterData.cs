using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SistemaRiego.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Week2MasterData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GlobalParameters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsEditable = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalParameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MasterCatalogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterCatalogItems", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "GlobalParameters",
                columns: new[] { "Id", "Category", "DataType", "Description", "IsEditable", "Key", "UpdatedAtUtc", "UpdatedByUserId", "Value" },
                values: new object[,]
                {
                    { new Guid("50000000-0000-0000-0000-000000000001"), "Telemetría", "integer", "Frecuencia predeterminada de envío de lecturas", true, "TELEMETRY_INTERVAL_SECONDS", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "60" },
                    { new Guid("50000000-0000-0000-0000-000000000002"), "Riego", "integer", "Duración predeterminada de un evento de riego", true, "DEFAULT_IRRIGATION_MINUTES", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "15" },
                    { new Guid("50000000-0000-0000-0000-000000000003"), "Seguridad", "integer", "Intentos fallidos antes del bloqueo temporal", true, "MAX_LOGIN_ATTEMPTS", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "5" },
                    { new Guid("50000000-0000-0000-0000-000000000004"), "Dispositivos", "integer", "Minutos sin lecturas para considerar un sensor desconectado", true, "SENSOR_OFFLINE_MINUTES", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "10" }
                });

            migrationBuilder.InsertData(
                table: "MasterCatalogItems",
                columns: new[] { "Id", "Code", "CreatedAtUtc", "Description", "IsActive", "Kind", "Name", "Symbol", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "SOIL_MOISTURE", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sensor para medir humedad volumétrica", true, 1, "Humedad del suelo", null, new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "AIR_TEMPERATURE", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sensor de temperatura del entorno", true, 1, "Temperatura ambiente", null, new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("20000000-0000-0000-0000-000000000001"), "RASPBERRY_PI", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Gateway y controlador central", true, 2, "Raspberry Pi", null, new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("20000000-0000-0000-0000-000000000002"), "SENSOR_NODE", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Nodo distribuido de adquisición", true, 2, "Nodo de sensores", null, new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "SOLENOID_VALVE", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Actuador para apertura y cierre del riego", true, 2, "Válvula solenoide", null, new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000001"), "PERCENT", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, 3, "Porcentaje", "%", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000002"), "CELSIUS", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, 3, "Grados Celsius", "°C", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000003"), "LITER", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, 3, "Litro", "L", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("40000000-0000-0000-0000-000000000001"), "ACTIVE", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, 4, "Activo", null, new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("40000000-0000-0000-0000-000000000002"), "OFFLINE", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, 4, "Sin conexión", null, new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("40000000-0000-0000-0000-000000000003"), "MAINTENANCE", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, 4, "En mantenimiento", null, new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("40000000-0000-0000-0000-000000000004"), "FAILURE", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, 4, "Con falla", null, new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_GlobalParameters_Key",
                table: "GlobalParameters",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MasterCatalogItems_Kind_Code",
                table: "MasterCatalogItems",
                columns: new[] { "Kind", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GlobalParameters");

            migrationBuilder.DropTable(
                name: "MasterCatalogItems");
        }
    }
}
