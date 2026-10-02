namespace Patamar.Gateway.External.Domain.Events;

public sealed class EventsDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public DateTimeOffset? StartDate { get; init; }
    public DateTimeOffset? EndDate { get; init; }
    public double? Lat { get; init; }
    public double? Long { get; init; }
    public required string Source { get; init; }
}
