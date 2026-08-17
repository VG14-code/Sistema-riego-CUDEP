using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SistemaRiego.Api.Data;

#nullable disable

namespace SistemaRiego.Api.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260810140000_Week14AnalyticsViews")]
public partial class Week14AnalyticsViews : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
CREATE OR ALTER VIEW dbo.vw_PowerBI_Consumption AS
SELECT c.Id AS ConsumptionId, c.RecordedAtUtc, CAST(c.RecordedAtUtc AS date) AS ConsumptionDate,
       DATEPART(year,c.RecordedAtUtc) AS [Year], DATEPART(month,c.RecordedAtUtc) AS [Month],
       DATEPART(iso_week,c.RecordedAtUtc) AS IsoWeek, c.VolumeLiters, c.FlowRateLitersMinute,
       c.DurationMinutes, c.Source, r.Mode, r.Status AS IrrigationStatus, z.Id AS ZoneId,
       z.Name AS ZoneName, s.Id AS SectorId, s.Name AS SectorName, b.Name AS BlockName, f.Name AS FarmName
FROM dbo.WaterConsumptionRecords c
JOIN dbo.IrrigationRuns r ON r.Id=c.IrrigationRunId
JOIN dbo.IrrigationZones z ON z.Id=c.IrrigationZoneId
JOIN dbo.IrrigationSectors s ON s.Id=z.IrrigationSectorId
JOIN dbo.FarmBlocks b ON b.Id=s.FarmBlockId
JOIN dbo.Farms f ON f.Id=b.FarmId;");

        migrationBuilder.Sql(@"
CREATE OR ALTER VIEW dbo.vw_PowerBI_Telemetry AS
SELECT r.Id AS ReadingId, r.CapturedAtUtc, CAST(r.CapturedAtUtc AS date) AS ReadingDate,
       r.Value, r.IsValid, r.ValidationStatus, r.BatteryPercent, r.SignalStrength,
       s.Id AS SensorId, s.Name AS SensorName, s.Code AS SensorCode,
       z.Id AS ZoneId, z.Name AS ZoneName, sec.Name AS SectorName
FROM dbo.SensorReadings r
JOIN dbo.IoTSensors s ON s.Id=r.SensorId
LEFT JOIN dbo.IrrigationZones z ON z.Id=r.IrrigationZoneId
LEFT JOIN dbo.IrrigationSectors sec ON sec.Id=z.IrrigationSectorId;");

        migrationBuilder.Sql(@"
CREATE OR ALTER VIEW dbo.vw_PowerBI_OperationalEvents AS
SELECT e.Id AS EventId, e.OccurredAtUtc, CAST(e.OccurredAtUtc AS date) AS EventDate,
       e.Category, e.EventType, e.Severity, e.UserEmail, e.Detail,
       z.Id AS ZoneId, z.Name AS ZoneName, s.Name AS SectorName
FROM dbo.OperationalEvents e
LEFT JOIN dbo.IrrigationZones z ON z.Id=e.IrrigationZoneId
LEFT JOIN dbo.IrrigationSectors s ON s.Id=z.IrrigationSectorId;");

        migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE dbo.sp_PowerBI_ConsumptionSummary
    @From datetime2 = NULL, @To datetime2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ConsumptionDate, SectorName, ZoneName, SUM(VolumeLiters) AS TotalLiters,
           COUNT_BIG(*) AS IrrigationEvents, AVG(VolumeLiters) AS AverageLiters
    FROM dbo.vw_PowerBI_Consumption
    WHERE (@From IS NULL OR RecordedAtUtc>=@From) AND (@To IS NULL OR RecordedAtUtc<DATEADD(day,1,@To))
    GROUP BY ConsumptionDate, SectorName, ZoneName
    ORDER BY ConsumptionDate, SectorName, ZoneName;
END;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_PowerBI_ConsumptionSummary;");
        migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_PowerBI_OperationalEvents;");
        migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_PowerBI_Telemetry;");
        migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_PowerBI_Consumption;");
    }
}
