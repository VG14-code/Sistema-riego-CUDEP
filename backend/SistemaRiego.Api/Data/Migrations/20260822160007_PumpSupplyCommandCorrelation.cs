using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaRiego.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PumpSupplyCommandCorrelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StartCommandId",
                table: "WaterSupplyEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StopCommandId",
                table: "WaterSupplyEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaterSupplyEvents_StartCommandId",
                table: "WaterSupplyEvents",
                column: "StartCommandId");

            migrationBuilder.CreateIndex(
                name: "IX_WaterSupplyEvents_StopCommandId",
                table: "WaterSupplyEvents",
                column: "StopCommandId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WaterSupplyEvents_StartCommandId",
                table: "WaterSupplyEvents");

            migrationBuilder.DropIndex(
                name: "IX_WaterSupplyEvents_StopCommandId",
                table: "WaterSupplyEvents");

            migrationBuilder.DropColumn(
                name: "StartCommandId",
                table: "WaterSupplyEvents");

            migrationBuilder.DropColumn(
                name: "StopCommandId",
                table: "WaterSupplyEvents");
        }
    }
}
