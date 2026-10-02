using Patamar.Gateway.External.Domain.Events;
using Patamar.Gateway.External.Domain.Radar;

namespace Patamar.Gateway.External.Application.Abstractions;

public interface IEventRepository
{
    Task UpsertManyAsync(IEnumerable<EventsDto> events, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EventsDto>> GetNearbyAsync(
        double lat,
        double lng,
        double radiusKm,
        CancellationToken cancellationToken = default);
}

public interface IRadarAreaRepository
{
    Task<RadarArea?> FindCoveringAreaAsync(
        double lat,
        double lng,
        DateTimeOffset minSyncedAt,
        CancellationToken cancellationToken = default);

    Task UpsertAreaAsync(RadarArea area, CancellationToken cancellationToken = default);
}
