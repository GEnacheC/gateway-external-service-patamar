using Patamar.Gateway.External.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Patamar.Gateway.External.Api.Controllers;

[ApiController]
[Route("api/events")]
public sealed class EventsController(EventService eventService) : ControllerBase
{
    /// <summary>
    /// Lists events already cached in MongoDB inside the city-sized radar around lat/long.
    /// Does not call external providers.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetEvents(
        [FromQuery] double? lat,
        [FromQuery(Name = "long")] double? lng,
        [FromQuery] double? radiusKm,
        CancellationToken cancellationToken)
    {
        if (lat is null || lng is null)
        {
            return BadRequest(new { error = "Query params 'lat' and 'long' are required." });
        }

        try
        {
            var result = await eventService.GetEventsAsync(lat.Value, lng.Value, radiusKm, cancellationToken);
            return Ok(new
            {
                lat = result.Lat,
                @long = result.Long,
                radiusKm = result.RadiusKm,
                count = result.Events.Count,
                events = result.Events
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Syncs external providers into a city-sized radar around lat/long.
    /// If the area was synced recently, skips external calls (use force=true to refresh).
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> Sync(
        [FromQuery] double? lat,
        [FromQuery(Name = "long")] double? lng,
        [FromQuery] string provider = EventService.AllProviders,
        [FromQuery] double? radiusKm = null,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (lat is null || lng is null)
        {
            return BadRequest(new { error = "Query params 'lat' and 'long' are required." });
        }

        try
        {
            var result = await eventService.SyncAsync(provider, lat.Value, lng.Value, radiusKm, force, cancellationToken);
            return Ok(new
            {
                provider,
                lat = result.Lat,
                @long = result.Long,
                radiusKm = result.RadiusKm,
                radarAreaId = result.RadarAreaId,
                skipped = result.Skipped,
                skipReason = result.SkipReason,
                cachedEvents = result.CachedEvents,
                synced = result.Synced,
                errors = result.Errors,
                total = result.Total
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }
}
