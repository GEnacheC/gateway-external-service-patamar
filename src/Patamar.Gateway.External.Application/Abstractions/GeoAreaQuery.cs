namespace Patamar.Gateway.External.Application.Abstractions;

public sealed class GeoAreaQuery
{
    public required double Lat { get; init; }
    public required double Long { get; init; }
    public double RadiusKm { get; init; } = 25;
    public string? Provider { get; init; }
}
