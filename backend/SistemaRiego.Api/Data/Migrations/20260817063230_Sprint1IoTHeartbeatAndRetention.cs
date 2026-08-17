using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaRiego.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint1IoTHeartbeatAndRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastCommunicationUtc",
                table: "IoTDevices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SensorReadings_ReceivedAtUtc",
                table: "SensorReadings",
                column: "ReceivedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SensorReadings_ReceivedAtUtc",
                table: "SensorReadings");

            migrationBuilder.DropColumn(
                name: "LastCommunicationUtc",
                table: "IoTDevices");
        }
    }
}
