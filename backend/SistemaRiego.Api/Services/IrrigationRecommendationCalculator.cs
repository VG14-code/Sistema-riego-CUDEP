namespace SistemaRiego.Api.Services;

public sealed record RecommendationInput(decimal? Moisture, decimal Minimum, decimal Target, decimal Maximum, int BaseDurationMinutes, decimal BaseVolumeLiters);
public sealed record RecommendationDecision(string Decision, int Minutes, decimal Liters, string Explanation);

public interface IIrrigationRecommendationCalculator { RecommendationDecision Calculate(RecommendationInput input); }

public sealed class IrrigationRecommendationCalculator : IIrrigationRecommendationCalculator
{
    public RecommendationDecision Calculate(RecommendationInput input)
    {
        if (!input.Moisture.HasValue) return new("Esperar lectura", 0, 0, "No existe una lectura válida para la zona.");
        if (input.Moisture.Value > input.Maximum) return new("Suspender riego", 0, 0, $"La humedad {input.Moisture:0.0}% supera el máximo {input.Maximum:0.0}%.");
        if (input.Moisture.Value >= input.Minimum) return new("Mantener", 0, 0, $"La humedad {input.Moisture:0.0}% está dentro del rango operativo.");
        var minutes = Math.Max(1, (int)Math.Ceiling(input.BaseDurationMinutes * (input.Target - input.Moisture.Value) / Math.Max(1, input.Target - input.Minimum)));
        var liters = input.BaseDurationMinutes <= 0 ? 0 : Math.Round(input.BaseVolumeLiters * minutes / input.BaseDurationMinutes, 1);
        return new("Regar", minutes, liters, $"La humedad {input.Moisture:0.0}% está debajo del mínimo {input.Minimum:0.0}%.");
    }
}
