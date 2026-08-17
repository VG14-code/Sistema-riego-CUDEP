using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Services;

public sealed class CropStageTransitionOptions
{
    public const string SectionName = "CropStageTransition";
    public int CheckIntervalMinutes { get; init; } = 60;
}

public sealed class CropStageTransitionWorker(IServiceScopeFactory scopeFactory, IOptions<CropStageTransitionOptions> options, ILogger<CropStageTransitionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await UpdateStagesAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, options.Value.CheckIntervalMinutes)));
        while (await timer.WaitForNextTickAsync(stoppingToken)) await UpdateStagesAsync(stoppingToken);
    }

    internal async Task UpdateStagesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cycles = await db.CropCycles.Include(x => x.Crop).ThenInclude(x => x.Stages).Where(x => x.Status == "Activo").ToListAsync(ct);
        var changed = 0;
        foreach (var cycle in cycles)
        {
            var elapsed = Math.Max(0, today.DayNumber - cycle.SowingDate.DayNumber);
            var cumulative = 0;
            var stage = cycle.Crop.Stages.OrderBy(x => x.Sequence).FirstOrDefault(x => { cumulative += x.EstimatedDays; return elapsed < cumulative; }) ?? cycle.Crop.Stages.OrderBy(x => x.Sequence).LastOrDefault();
            if (stage is not null && cycle.CurrentStageId != stage.Id)
            {
                cycle.CurrentStageId = stage.Id;
                db.AccessAudits.Add(new AccessAudit { EventType = "CROP_STAGE_AUTO_CHANGED", Detail = $"{cycle.Name}: {stage.Name}" });
                changed++;
            }
            if (today > cycle.ExpectedHarvestDate && cycle.ActualHarvestDate is null && cycle.Status != "Vencido") { cycle.Status = "Vencido"; changed++; }
        }
        if (changed > 0) { await db.SaveChangesAsync(ct); logger.LogInformation("Planificación actualizó {Count} ciclos/etapas automáticamente", changed); }
    }
}
