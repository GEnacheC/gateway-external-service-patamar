using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Patamar.Gateway.External.Application.Abstractions;
using Patamar.Gateway.External.Domain.Events;
using Patamar.Gateway.External.Infrastructure.Configuration;

namespace Patamar.Gateway.External.Infrastructure.Ticketmaster;

public sealed class TicketmasterService(
    HttpClient httpClient,
    IOptions<TicketmasterOptions> options,
    ILogger<TicketmasterService> logger) : IExternalService
{
    public const string Provider = "ticketmaster";

    public string ProviderName => Provider;

    public async Task<IReadOnlyList<EventsDto>> GetEventsAsync(
        GetEventsQuery query,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        if (string.IsNullOrWhiteSpace(cfg.ApiKey))
        {
            throw new InvalidOperationException(
                "Ticketmaster API key is not configured. Set Ticketmaster:ApiKey or TICKETMASTER_API_KEY.");
        }

        var lat = query.Lat;
        var lon = query.Long;
        var radius = query.RadiusKm > 0 ? query.RadiusKm : cfg.RadiusKm;
        var pageSize = Math.Clamp(cfg.PageSize, 1, 200);
        var maxPages = Math.Max(1, cfg.MaxPages);

        var results = new List<EventsDto>();
        var page = 0;
        var totalPages = 1;

        while (page < totalPages && page < maxPages)
        {
            var url =
                $"events.json?apikey={Uri.EscapeDataString(cfg.ApiKey)}" +
                $"&latlong={lat.ToString(CultureInfo.InvariantCulture)},{lon.ToString(CultureInfo.InvariantCulture)}" +
                $"&radius={((int)Math.Ceiling(radius)).ToString(CultureInfo.InvariantCulture)}" +
                $"&unit=km&size={pageSize.ToString(CultureInfo.InvariantCulture)}" +
                $"&page={page.ToString(CultureInfo.InvariantCulture)}" +
                "&sort=date,asc";

            using var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<TicketmasterResponse>(cancellationToken: cancellationToken)
                ?? new TicketmasterResponse();

            totalPages = payload.Page?.TotalPages ?? 1;
            var events = payload.Embedded?.Events ?? [];
            if (events.Count == 0)
            {
                break;
            }

            results.AddRange(events.Select(Map).Where(x => x is not null)!);
            page++;
        }

        logger.LogInformation("Fetched {Count} Ticketmaster events near {Lat},{Lon}", results.Count, lat, lon);
        return results;
    }

    private static EventsDto? Map(TicketmasterEvent source)
    {
        if (string.IsNullOrWhiteSpace(source.Id) || string.IsNullOrWhiteSpace(source.Name))
        {
            return null;
        }

        var venue = source.Embedded?.Venues?.FirstOrDefault();
        double? lat = null;
        double? lon = null;
        if (double.TryParse(venue?.Location?.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLat))
        {
            lat = parsedLat;
        }

        if (double.TryParse(venue?.Location?.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLon))
        {
            lon = parsedLon;
        }

        return new EventsDto
        {
            Id = source.Id,
            Name = source.Name,
            StartDate = ParseDate(source.Dates?.Start?.DateTime) ?? ParseDate(source.Dates?.Start?.LocalDate),
            EndDate = ParseDate(source.Dates?.End?.DateTime) ?? ParseDate(source.Dates?.End?.LocalDate),
            Lat = lat,
            Long = lon,
            Source = Provider
        };
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto)
            ? dto
            : null;
    }

    private sealed class TicketmasterResponse
    {
        [JsonPropertyName("_embedded")]
        public TicketmasterEmbedded? Embedded { get; set; }

        [JsonPropertyName("page")]
        public TicketmasterPage? Page { get; set; }
    }

    private sealed class TicketmasterEmbedded
    {
        [JsonPropertyName("events")]
        public List<TicketmasterEvent>? Events { get; set; }
    }

    private sealed class TicketmasterPage
    {
        [JsonPropertyName("totalPages")]
        public int TotalPages { get; set; }
    }

    private sealed class TicketmasterEvent
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("dates")]
        public TicketmasterDates? Dates { get; set; }

        [JsonPropertyName("_embedded")]
        public TicketmasterEventEmbedded? Embedded { get; set; }
    }

    private sealed class TicketmasterDates
    {
        [JsonPropertyName("start")]
        public TicketmasterDatePoint? Start { get; set; }

        [JsonPropertyName("end")]
        public TicketmasterDatePoint? End { get; set; }
    }

    private sealed class TicketmasterDatePoint
    {
        [JsonPropertyName("localDate")]
        public string? LocalDate { get; set; }

        [JsonPropertyName("dateTime")]
        public string? DateTime { get; set; }
    }

    private sealed class TicketmasterEventEmbedded
    {
        [JsonPropertyName("venues")]
        public List<TicketmasterVenue>? Venues { get; set; }
    }

    private sealed class TicketmasterVenue
    {
        [JsonPropertyName("location")]
        public TicketmasterLocation? Location { get; set; }
    }

    private sealed class TicketmasterLocation
    {
        [JsonPropertyName("latitude")]
        public string? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public string? Longitude { get; set; }
    }
}
