using System.Text.Json;

namespace SistemaRiego.Api.Services;

public sealed class SpatialGeometryValidator
{
    private const double Epsilon = 1e-10;

    public IReadOnlyList<GeoPoint> ParsePolygon(string? geoJson, string label)
    {
        if (string.IsNullOrWhiteSpace(geoJson)) return [];
        try
        {
            using var document = JsonDocument.Parse(geoJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var type) || !string.Equals(type.GetString(), "Polygon", StringComparison.OrdinalIgnoreCase))
                throw new SpatialValidationException($"El límite de {label} debe ser un GeoJSON Polygon.");
            var ring = root.GetProperty("coordinates")[0];
            var points = ring.EnumerateArray().Select(value => new GeoPoint(value[0].GetDouble(), value[1].GetDouble())).ToList();
            if (points.Count < 4) throw new SpatialValidationException($"El polígono de {label} necesita al menos tres vértices y cierre.");
            if (!Same(points[0], points[^1])) throw new SpatialValidationException($"El polígono de {label} debe estar cerrado.");
            if (Math.Abs(SignedArea(points)) < Epsilon) throw new SpatialValidationException($"El polígono de {label} no puede tener área cero.");
            if (HasSelfIntersection(points)) throw new SpatialValidationException($"El polígono de {label} no puede cruzarse a sí mismo.");
            return points;
        }
        catch (SpatialValidationException) { throw; }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new SpatialValidationException($"El límite de {label} no contiene un GeoJSON Polygon válido.");
        }
    }

    public void EnsureZoneInsideSector(IReadOnlyList<GeoPoint> zone, IReadOnlyList<GeoPoint> sector)
    {
        if (zone.Count == 0) return;
        if (sector.Count == 0) throw new SpatialValidationException("El sector debe tener un polígono antes de asignar límites a sus zonas.");
        if (zone.Take(zone.Count - 1).Any(point => !ContainsOrBoundary(sector, point)))
            throw new SpatialValidationException("El polígono de la zona debe estar completamente dentro de su sector.");
        for (var i = 0; i < zone.Count - 1; i++)
            for (var j = 0; j < sector.Count - 1; j++)
                if (ProperlyIntersects(zone[i], zone[i + 1], sector[j], sector[j + 1]))
                    throw new SpatialValidationException("El polígono de la zona cruza el límite de su sector.");
    }

    public bool Overlaps(IReadOnlyList<GeoPoint> first, IReadOnlyList<GeoPoint> second)
    {
        if (first.Count == 0 || second.Count == 0) return false;
        for (var i = 0; i < first.Count - 1; i++)
            for (var j = 0; j < second.Count - 1; j++)
                if (ProperlyIntersects(first[i], first[i + 1], second[j], second[j + 1])) return true;
        return first.Take(first.Count - 1).Any(point => StrictlyContains(second, point))
            || second.Take(second.Count - 1).Any(point => StrictlyContains(first, point));
    }

    private static bool HasSelfIntersection(IReadOnlyList<GeoPoint> polygon)
    {
        for (var i = 0; i < polygon.Count - 1; i++)
            for (var j = i + 1; j < polygon.Count - 1; j++)
            {
                if (Math.Abs(i - j) <= 1 || (i == 0 && j == polygon.Count - 2)) continue;
                if (Intersects(polygon[i], polygon[i + 1], polygon[j], polygon[j + 1])) return true;
            }
        return false;
    }

    private static bool StrictlyContains(IReadOnlyList<GeoPoint> polygon, GeoPoint point) => ContainsOrBoundary(polygon, point) && !OnBoundary(polygon, point);
    private static bool ContainsOrBoundary(IReadOnlyList<GeoPoint> polygon, GeoPoint point)
    {
        if (OnBoundary(polygon, point)) return true;
        var inside = false;
        for (var i = 0; i < polygon.Count - 1; i++)
        {
            var a = polygon[i]; var b = polygon[i + 1];
            if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }

    private static bool OnBoundary(IReadOnlyList<GeoPoint> polygon, GeoPoint point)
    {
        for (var i = 0; i < polygon.Count - 1; i++) if (OnSegment(polygon[i], point, polygon[i + 1])) return true;
        return false;
    }

    private static bool Intersects(GeoPoint a, GeoPoint b, GeoPoint c, GeoPoint d)
    {
        var o1 = Orientation(a, b, c); var o2 = Orientation(a, b, d); var o3 = Orientation(c, d, a); var o4 = Orientation(c, d, b);
        return o1 != o2 && o3 != o4 || o1 == 0 && OnSegment(a, c, b) || o2 == 0 && OnSegment(a, d, b) || o3 == 0 && OnSegment(c, a, d) || o4 == 0 && OnSegment(c, b, d);
    }

    private static bool ProperlyIntersects(GeoPoint a, GeoPoint b, GeoPoint c, GeoPoint d) => Orientation(a, b, c) * Orientation(a, b, d) < 0 && Orientation(c, d, a) * Orientation(c, d, b) < 0;
    private static int Orientation(GeoPoint a, GeoPoint b, GeoPoint c)
    {
        var value = (b.Y - a.Y) * (c.X - b.X) - (b.X - a.X) * (c.Y - b.Y);
        return Math.Abs(value) < Epsilon ? 0 : value > 0 ? 1 : -1;
    }
    private static bool OnSegment(GeoPoint a, GeoPoint p, GeoPoint b) => Orientation(a, p, b) == 0 && p.X <= Math.Max(a.X, b.X) + Epsilon && p.X >= Math.Min(a.X, b.X) - Epsilon && p.Y <= Math.Max(a.Y, b.Y) + Epsilon && p.Y >= Math.Min(a.Y, b.Y) - Epsilon;
    private static double SignedArea(IReadOnlyList<GeoPoint> polygon) { var area = 0d; for (var i = 0; i < polygon.Count - 1; i++) area += polygon[i].X * polygon[i + 1].Y - polygon[i + 1].X * polygon[i].Y; return area / 2; }
    private static bool Same(GeoPoint a, GeoPoint b) => Math.Abs(a.X - b.X) < Epsilon && Math.Abs(a.Y - b.Y) < Epsilon;
}

public readonly record struct GeoPoint(double X, double Y);
public sealed class SpatialValidationException(string message) : Exception(message);
