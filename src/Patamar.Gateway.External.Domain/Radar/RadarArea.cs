namespace Patamar.Gateway.External.Domain.Radar;

public sealed class RadarArea
{
    public required string Id { get; init; }
    public required double Lat { get; init; }
    public required double Long { get; init; }
    public required double RadiusKm { get; init; }
    public required DateTimeOffset LastSyncedAt { get; init; }
    public IReadOnlyList<string> Providers { get; init; } = [];
}
