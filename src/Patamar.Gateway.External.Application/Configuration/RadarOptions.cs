namespace Patamar.Gateway.External.Application.Configuration;

public sealed class RadarOptions
{
    public const string SectionName = "Radar";

    /// <summary>Default search/sync radius for a medium-sized city (km).</summary>
    public double CityRadiusKm { get; set; } = 25;

    /// <summary>How long a synced radar area remains warm before another sync is needed.</summary>
    public int SyncTtlHours { get; set; } = 6;

    public string RadarAreasCollectionName { get; set; } = "radar_areas";
}
