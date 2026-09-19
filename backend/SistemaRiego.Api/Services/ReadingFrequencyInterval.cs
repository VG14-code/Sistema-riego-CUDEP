using System.Text.RegularExpressions;

namespace SistemaRiego.Api.Services;

// Convierte a segundos las frecuencias que se registraron solo como texto
// («60 s», «5 min», «1 h», «Cada minuto») antes de existir IntervalSeconds.
public static partial class ReadingFrequencyInterval
{
    public static int? Parse(string? text)
    {
        var match = Pattern().Match(text ?? string.Empty);
        if (!match.Success) return null;
        var amount = match.Groups["amount"].Success ? int.Parse(match.Groups["amount"].Value) : 1;
        var unit = match.Groups["unit"].Value.ToLowerInvariant();
        var seconds = unit.StartsWith('h') ? amount * 3600 : unit.StartsWith('m') ? amount * 60 : amount;
        return seconds is >= 1 and <= 3600 ? seconds : null;
    }

    [GeneratedRegex(@"(?:(?<amount>\d+)\s*|\b)(?<unit>h(?:oras?)?|min(?:utos?)?|s(?:eg(?:undos?)?)?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Pattern();
}
