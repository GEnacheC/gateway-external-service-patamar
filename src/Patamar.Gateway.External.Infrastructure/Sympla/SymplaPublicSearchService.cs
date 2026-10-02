using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Patamar.Gateway.External.Application.Abstractions;
using Patamar.Gateway.External.Domain.Events;
using Patamar.Gateway.External.Infrastructure.Configuration;

namespace Patamar.Gateway.External.Infrastructure.Sympla;

public sealed class SymplaPublicSearchService(
    HttpClient httpClient,
    IOptions<SymplaPublicOptions> options,
    ILogger<SymplaPublicSearchService> logger) : IExternalService
{
    public const string Provider = "sympla-public";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly Regex EventIdRegex = new(@"/(\d+)/?$", RegexOptions.Compiled);

    public string ProviderName => Provider;

    public async Task<IReadOnlyList<EventsDto>> GetEventsAsync(
        GetEventsQuery query,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        var city = string.IsNullOrWhiteSpace(query.City) ? cfg.City : query.City;
        var state = string.IsNullOrWhiteSpace(query.State) ? cfg.State : query.State;
        var pageSize = Math.Clamp(cfg.PageSize, 1, 100);
        var maxPages = Math.Max(1, cfg.MaxPages);

        var results = new List<EventsDto>();
        var page = 1;
        var total = int.MaxValue;

        while (page <= maxPages && results.Count < total)
        {
            using var content = JsonContent.Create(new Dictionary<string, object?>
            {
                ["service"] = "/v4/search",
                ["params"] = new Dictionary<string, object?>
                {
                    ["city"] = city,
                    ["state"] = state,
                    ["only"] = "name,location,start_date_formats,end_date_formats,url",
                    ["sort"] = "date",
                    ["order_by"] = "asc",
                    ["limit"] = pageSize.ToString(CultureInfo.InvariantCulture),
                    ["page"] = page
                },
                ["ignoreLocation"] = true
            });

            using var response = await httpClient.PostAsync(cfg.SearchUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<SymplaPublicResponse>(JsonOptions, cancellationToken)
                ?? new SymplaPublicResponse();

            total = payload.Total ?? payload.Data?.Count ?? 0;
            if (payload.Data is { Count: > 0 })
            {
                results.AddRange(payload.Data.Select(Map).Where(x => x is not null)!);
            }
            else
            {
                break;
            }

            if (results.Count >= total || payload.Data.Count < pageSize)
            {
                break;
            }

            page++;
        }

        logger.LogInformation("Fetched {Count} public Sympla events for {City}/{State}", results.Count, city, state);
        return results;
    }

    private static EventsDto? Map(SymplaPublicEvent item)
    {
        var id = ExtractId(item.Url);
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(item.Name))
        {
            return null;
        }

        double? lat = item.Location?.Lat;
        double? lon = item.Location?.Lon;
        if ((lat is null or 0) && (lon is null or 0))
        {
            lat = null;
            lon = null;
        }

        return new EventsDto
        {
            Id = id,
            Name = item.Name,
            StartDate = ParseLocalizedDate(item.StartDateFormats?.Pt),
            EndDate = ParseLocalizedDate(item.EndDateFormats?.Pt),
            Lat = lat,
            Long = lon,
            Source = Provider
        };
    }

    private static string? ExtractId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var match = EventIdRegex.Match(url);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static DateTimeOffset? ParseLocalizedDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Example: "Qua, 21 Jun - 2017 · 18:00"
        var normalized = value.Replace('·', ' ').Replace(" - ", " ");
        var match = Regex.Match(
            normalized,
            @"(\d{1,2})\s+([A-Za-zçÇáéíóúãõâêôÁÉÍÓÚÃÕÂÊÔ]+)\s+(\d{4})\s+(\d{1,2}:\d{2})",
            RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            return null;
        }

        var day = match.Groups[1].Value;
        var monthToken = match.Groups[2].Value;
        var year = match.Groups[3].Value;
        var time = match.Groups[4].Value;
        var month = MonthFromPt(monthToken);
        if (month is null)
        {
            return null;
        }

        var raw = $"{year}-{month:00}-{int.Parse(day, CultureInfo.InvariantCulture):00} {time}";
        return DateTimeOffset.TryParseExact(
            raw,
            "yyyy-MM-dd HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var dto)
            ? dto
            : null;
    }

    private static int? MonthFromPt(string token)
    {
        var t = token.Trim().ToLowerInvariant();
        return t switch
        {
            "jan" or "janeiro" => 1,
            "fev" or "fevereiro" => 2,
            "mar" or "marco" or "março" => 3,
            "abr" or "abril" => 4,
            "mai" or "maio" => 5,
            "jun" or "junho" => 6,
            "jul" or "julho" => 7,
            "ago" or "agosto" => 8,
            "set" or "setembro" => 9,
            "out" or "outubro" => 10,
            "nov" or "novembro" => 11,
            "dez" or "dezembro" => 12,
            _ => null
        };
    }

    private sealed class SymplaPublicResponse
    {
        [JsonPropertyName("data")]
        public List<SymplaPublicEvent>? Data { get; set; }

        [JsonPropertyName("total")]
        public int? Total { get; set; }
    }

    private sealed class SymplaPublicEvent
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("location")]
        public SymplaPublicLocation? Location { get; set; }

        [JsonPropertyName("start_date_formats")]
        public SymplaDateFormats? StartDateFormats { get; set; }

        [JsonPropertyName("end_date_formats")]
        public SymplaDateFormats? EndDateFormats { get; set; }
    }

    private sealed class SymplaPublicLocation
    {
        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lon")]
        public double Lon { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }
    }

    private sealed class SymplaDateFormats
    {
        [JsonPropertyName("pt")]
        public string? Pt { get; set; }
    }
}
