using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaRiego.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompleteCalibrationTraceability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CalibrationPattern",
                table: "SensorCalibrations",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "NextCalibrationDate",
                table: "SensorCalibrations",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicianName",
                table: "SensorCalibrations",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CalibrationPattern",
                table: "SensorCalibrations");

            migrationBuilder.DropColumn(
                name: "NextCalibrationDate",
                table: "SensorCalibrations");

            migrationBuilder.DropColumn(
                name: "TechnicianName",
                table: "SensorCalibrations");
        }
    }
}
