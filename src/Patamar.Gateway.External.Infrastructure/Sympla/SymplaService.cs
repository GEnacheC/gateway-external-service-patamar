using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Patamar.Gateway.External.Application.Abstractions;
using Patamar.Gateway.External.Domain.Events;
using Patamar.Gateway.External.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Patamar.Gateway.External.Infrastructure.Sympla;

public sealed class SymplaService(
    HttpClient httpClient,
    IOptions<SymplaOptions> options,
    ILogger<SymplaService> logger) : IExternalService
{
    public const string Provider = "sympla";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string ProviderName => Provider;

    public async Task<IReadOnlyList<EventsDto>> GetEventsAsync(
        GetEventsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Token))
        {
            throw new InvalidOperationException(
                "Sympla token is not configured. Set Sympla:Token or SYMPLA_TOKEN.");
        }

        var results = new List<EventsDto>();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var hasNext = true;

        while (hasNext)
        {
            var url = $"events?page={page}&page_size={pageSize}";
            using var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<SymplaEventsResponse>(JsonOptions, cancellationToken)
                ?? new SymplaEventsResponse();

            if (payload.Data is { Count: > 0 })
            {
                results.AddRange(payload.Data.Select(Map));
            }

            hasNext = payload.Pagination?.HasNext == true;
            page++;

            if (payload.Data is null || payload.Data.Count == 0)
            {
                break;
            }
        }

        logger.LogInformation("Fetched {Count} events from Sympla", results.Count);
        return results;
    }

    private static EventsDto Map(SymplaEventData source)
    {
        var lon = source.Address?.Lon ?? source.Address?.Long;

        return new EventsDto
        {
            Id = source.Id.ToString(CultureInfo.InvariantCulture),
            Name = source.Name ?? string.Empty,
            StartDate = ParseDate(source.StartDate),
            EndDate = ParseDate(source.EndDate),
            Lat = ParseCoordinate(source.Address?.Lat),
            Long = ParseCoordinate(lon),
            Source = Provider
        };
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dto))
        {
            return dto;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
        {
            return new DateTimeOffset(dt);
        }

        return null;
    }

    private static double? ParseCoordinate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }
}
