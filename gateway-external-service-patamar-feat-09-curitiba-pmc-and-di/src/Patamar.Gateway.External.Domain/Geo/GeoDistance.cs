namespace Patamar.Gateway.External.Domain.Geo;

public static class GeoDistance
{
    private const double EarthRadiusKm = 6371.0;

    public static double Kilometers(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    public static bool IsWithinRadius(
        double originLat,
        double originLon,
        double? pointLat,
        double? pointLon,
        double radiusKm)
    {
        if (pointLat is null || pointLon is null)
        {
            return false;
        }

        return Kilometers(originLat, originLon, pointLat.Value, pointLon.Value) <= radiusKm;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
