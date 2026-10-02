namespace Patamar.Gateway.External.Application.Abstractions;

public sealed class GetEventsQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 100;
    public string City { get; init; } = "Curitiba";
    public string State { get; init; } = "PR";
    public required double Lat { get; init; }
    public required double Long { get; init; }
    public double RadiusKm { get; init; } = 25;
}
