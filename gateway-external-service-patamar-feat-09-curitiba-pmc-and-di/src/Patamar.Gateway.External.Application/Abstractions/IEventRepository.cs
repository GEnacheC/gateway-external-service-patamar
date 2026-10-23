using Patamar.Gateway.External.Domain.Events;
using Patamar.Gateway.External.Domain.Radar;

namespace Patamar.Gateway.External.Application.Abstractions;

public interface IEventRepository
{
    Task UpsertManyAsync(IEnumerable<EventsDto> events, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes cached events for a provider inside the radar circle so a refresh does not keep stale items.
    /// </summary>
    Task DeleteNearbyAsync(
        double lat,
        double lng,
        double radiusKm,
        string source,
        CancellationToken cancellationToken = default);

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
