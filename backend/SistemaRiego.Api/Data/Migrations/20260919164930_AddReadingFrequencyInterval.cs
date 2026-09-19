using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaRiego.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReadingFrequencyInterval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IntervalSeconds",
                table: "MasterCatalogItems",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                column: "IntervalSeconds",
                value: null);

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                column: "IntervalSeconds",
                value: null);

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                column: "IntervalSeconds",
                value: null);

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"),
                column: "IntervalSeconds",
                value: null);

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"),
                column: "IntervalSeconds",
                value: null);

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"),
                column: "IntervalSeconds",
                value: null);

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                column: "IntervalSeconds",
                value: null);

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000002"),
                column: "IntervalSeconds",
                value: null);

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000003"),
                column: "IntervalSeconds",
                value: null);

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000001"),
                column: "IntervalSeconds",
                value: null);

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000002"),
                column: "IntervalSeconds",
                value: null);

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000003"),
                column: "IntervalSeconds",
                value: null);

            migrationBuilder.UpdateData(
                table: "MasterCatalogItems",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000004"),
                column: "IntervalSeconds",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IntervalSeconds",
                table: "MasterCatalogItems");
        }
    }
}
