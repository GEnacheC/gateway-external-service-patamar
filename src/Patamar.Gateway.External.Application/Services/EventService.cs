using Patamar.Gateway.External.Application.Abstractions;
using Patamar.Gateway.External.Application.Configuration;
using Patamar.Gateway.External.Domain.Events;
using Patamar.Gateway.External.Domain.Geo;
using Patamar.Gateway.External.Domain.Radar;
using Microsoft.Extensions.Options;

namespace Patamar.Gateway.External.Application.Services;

public sealed class EventService(
    IExternalServiceFactory externalServiceFactory,
    IEventRepository eventRepository,
    IRadarAreaRepository radarAreaRepository,
    IOptions<RadarOptions> radarOptions)
{
    public const string AllProviders = "all";

    public async Task<GetEventsResult> GetEventsAsync(
        double lat,
        double lng,
        double? radiusKm = null,
        CancellationToken cancellationToken = default)
    {
        ValidateCoordinates(lat, lng);
        var radius = ResolveRadius(radiusKm);
        var events = await eventRepository.GetNearbyAsync(lat, lng, radius, cancellationToken);
        return new GetEventsResult
        {
            Lat = lat,
            Long = lng,
            RadiusKm = radius,
            Events = events
        };
    }

    public async Task<SyncResult> SyncAsync(
        string provider,
        double lat,
        double lng,
        double? radiusKm = null,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ValidateCoordinates(lat, lng);

        var cfg = radarOptions.Value;
        var radius = ResolveRadius(radiusKm);
        var result = new SyncResult
        {
            Lat = lat,
            Long = lng,
            RadiusKm = radius
        };

        if (!force)
        {
            var minSyncedAt = DateTimeOffset.UtcNow.AddHours(-Math.Max(1, cfg.SyncTtlHours));
            var existing = await radarAreaRepository.FindCoveringAreaAsync(lat, lng, minSyncedAt, cancellationToken);
            if (existing is not null)
            {
                var cached = await eventRepository.GetNearbyAsync(lat, lng, radius, cancellationToken);
                result.Skipped = true;
                result.SkipReason =
                    $"Area already covered by radar '{existing.Id}' synced at {existing.LastSyncedAt:O}. Use force=true to refresh.";
                result.CachedEvents = cached.Count;
                result.RadarAreaId = existing.Id;
                return result;
            }
        }

        var query = new GetEventsQuery
        {
            Lat = lat,
            Long = lng,
            RadiusKm = radius
        };

        if (string.Equals(provider, AllProviders, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var name in externalServiceFactory.GetAvailableProviders())
            {
                await SyncOneAsync(name, query, result, cancellationToken);
            }
        }
        else
        {
            await SyncOneAsync(provider, query, result, cancellationToken);
            if (result.Errors.Count > 0 && result.Total == 0)
            {
                throw new InvalidOperationException(result.Errors[provider]);
            }
        }

        var radarId = BuildRadarId(lat, lng, radius);
        await radarAreaRepository.UpsertAreaAsync(
            new RadarArea
            {
                Id = radarId,
                Lat = lat,
                Long = lng,
                RadiusKm = radius,
                LastSyncedAt = DateTimeOffset.UtcNow,
                Providers = result.Synced.Keys.ToArray()
            },
            cancellationToken);

        result.RadarAreaId = radarId;
        return result;
    }

    private async Task SyncOneAsync(
        string provider,
        GetEventsQuery query,
        SyncResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            var externalService = externalServiceFactory.Create(provider);
            var events = await externalService.GetEventsAsync(query, cancellationToken);

            var inRadius = events
                .Where(e => GeoDistance.IsWithinRadius(query.Lat, query.Long, e.Lat, e.Long, query.RadiusKm))
                .ToList();

            // Keep events without coordinates only for city-scoped providers that returned them;
            // radar list requires geo, so drop null-location items.
            await eventRepository.UpsertManyAsync(inRadius, cancellationToken);
            result.Synced[provider] = inRadius.Count;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result.Errors[provider] = ex.Message;
            result.Synced[provider] = 0;
        }
    }

    private double ResolveRadius(double? radiusKm)
    {
        if (radiusKm is > 0)
        {
            return Math.Clamp(radiusKm.Value, 1, 100);
        }

        return Math.Clamp(radarOptions.Value.CityRadiusKm, 1, 100);
    }

    private static void ValidateCoordinates(double lat, double lng)
    {
        if (lat is < -90 or > 90)
        {
            throw new ArgumentException("lat must be between -90 and 90.", nameof(lat));
        }

        if (lng is < -180 or > 180)
        {
            throw new ArgumentException("long must be between -180 and 180.", nameof(lng));
        }
    }

    private static string BuildRadarId(double lat, double lng, double radiusKm)
    {
        // Quantize to ~1km grid so nearby users share the same radar cell.
        var qLat = Math.Round(lat, 2);
        var qLng = Math.Round(lng, 2);
        var qRadius = Math.Round(radiusKm, 0);
        return $"radar:{qLat}:{qLng}:{qRadius}";
    }
}

public sealed class GetEventsResult
{
    public double Lat { get; set; }
    public double Long { get; set; }
    public double RadiusKm { get; set; }
    public IReadOnlyList<EventsDto> Events { get; set; } = [];
}

public sealed class SyncResult
{
    public Dictionary<string, int> Synced { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Errors { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int Total => Synced.Values.Sum();
    public double Lat { get; set; }
    public double Long { get; set; }
    public double RadiusKm { get; set; }
    public bool Skipped { get; set; }
    public string? SkipReason { get; set; }
    public int? CachedEvents { get; set; }
    public string? RadarAreaId { get; set; }
}
