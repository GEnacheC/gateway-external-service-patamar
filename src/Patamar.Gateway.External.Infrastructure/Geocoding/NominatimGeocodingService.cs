using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Patamar.Gateway.External.Infrastructure.Configuration;

namespace Patamar.Gateway.External.Infrastructure.Geocoding;

public interface IGeocodingService
{
    Task<(double? Lat, double? Long)> GeocodeAsync(string address, CancellationToken cancellationToken = default);
}

public sealed class NominatimGeocodingService(
    IHttpClientFactory httpClientFactory,
    IOptions<GeocodingOptions> options,
    ILogger<NominatimGeocodingService> logger) : IGeocodingService
{
    private static readonly ConcurrentDictionary<string, (double? Lat, double? Long)> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTimeOffset _lastCall = DateTimeOffset.MinValue;

    public async Task<(double? Lat, double? Long)> GeocodeAsync(
        string address,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return (null, null);
        }

        var key = address.Trim();
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (Cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            var minInterval = TimeSpan.FromMilliseconds(Math.Max(0, options.Value.MinIntervalMs));
            var elapsed = DateTimeOffset.UtcNow - _lastCall;
            if (elapsed < minInterval)
            {
                await Task.Delay(minInterval - elapsed, cancellationToken);
            }

            var client = httpClientFactory.CreateClient("nominatim");
            var url = $"search?q={Uri.EscapeDataString(key)}&format=json&limit=1";
            using var response = await client.GetAsync(url, cancellationToken);
            _lastCall = DateTimeOffset.UtcNow;

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Nominatim returned {Status} for {Address}", response.StatusCode, key);
                Cache[key] = (null, null);
                return (null, null);
            }

            var payload = await response.Content.ReadFromJsonAsync<List<NominatimResult>>(cancellationToken: cancellationToken);
            var first = payload?.FirstOrDefault();
            if (first is null
                || !double.TryParse(first.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                || !double.TryParse(first.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
            {
                Cache[key] = (null, null);
                return (null, null);
            }

            var coords = (lat, lon);
            Cache[key] = coords;
            return coords;
        }
        finally
        {
            Gate.Release();
        }
    }

    private sealed class NominatimResult
    {
        [JsonPropertyName("lat")]
        public string? Lat { get; set; }

        [JsonPropertyName("lon")]
        public string? Lon { get; set; }
    }
}
