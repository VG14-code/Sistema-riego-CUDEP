using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Tests;

public sealed class Sprint2RecommendationTests
{
    private readonly IrrigationRecommendationCalculator calculator = new();

    [Fact] public void NoReading_Waits() => Assert.Equal("Esperar lectura", calculator.Calculate(new(null, 40, 55, 70, 15, 120)).Decision);
    [Fact] public void AboveMaximum_Suspends() => Assert.Equal("Suspender riego", calculator.Calculate(new(75, 40, 55, 70, 15, 120)).Decision);
    [Fact] public void InsideRange_Maintains() => Assert.Equal("Mantener", calculator.Calculate(new(48, 40, 55, 70, 15, 120)).Decision);
    [Fact] public void BelowMinimum_RecommendsProportionalWater()
    {
        var result = calculator.Calculate(new(25, 40, 55, 70, 15, 120));
        Assert.Equal("Regar", result.Decision);
        Assert.Equal(30, result.Minutes);
        Assert.Equal(240, result.Liters);
    }
}
