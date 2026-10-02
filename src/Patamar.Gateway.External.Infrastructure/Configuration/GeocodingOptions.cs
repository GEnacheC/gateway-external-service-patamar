namespace Patamar.Gateway.External.Infrastructure.Configuration;

public sealed class GeocodingOptions
{
    public const string SectionName = "Geocoding";

    public string NominatimBaseUrl { get; set; } = "https://nominatim.openstreetmap.org/";
    public string UserAgent { get; set; } = "PatamarGatewayExternal/1.0 (events-gateway)";
    public int MinIntervalMs { get; set; } = 1100;
}
