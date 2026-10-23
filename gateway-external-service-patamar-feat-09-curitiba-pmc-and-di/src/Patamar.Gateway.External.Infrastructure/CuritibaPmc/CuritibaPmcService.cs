using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Patamar.Gateway.External.Application.Abstractions;
using Patamar.Gateway.External.Domain.Events;
using Patamar.Gateway.External.Infrastructure.Configuration;
using Patamar.Gateway.External.Infrastructure.Geocoding;

namespace Patamar.Gateway.External.Infrastructure.CuritibaPmc;

public sealed class CuritibaPmcService(
    HttpClient httpClient,
    IGeocodingService geocodingService,
    IOptions<CuritibaPmcOptions> options,
    ILogger<CuritibaPmcService> logger) : IExternalService
{
    public const string Provider = "curitiba-pmc";
    private static readonly Regex CsvHrefRegex = new(
        @"https://mid-dadosabertos\.curitiba\.pr\.gov\.br/agendapmc/\d{4}-\d{2}-\d{2}_Eventos_-_Base_de_Dados\.csv",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string ProviderName => Provider;

    public async Task<IReadOnlyList<EventsDto>> GetEventsAsync(
        GetEventsQuery query,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        var csvUrl = await ResolveLatestCsvUrlAsync(cfg, cancellationToken);
        logger.LogInformation("Downloading Curitiba PMC events from {Url}", csvUrl);

        await using var stream = await httpClient.GetStreamAsync(csvUrl, cancellationToken);
        using var reader = new StreamReader(stream, Encoding.GetEncoding("ISO-8859-1"));
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ";",
            BadDataFound = null,
            MissingFieldFound = null,
            HeaderValidated = null
        });

        var rows = csv.GetRecords<PmcEventRow>().ToList();
        var rowsById = rows
            .Where(r => long.TryParse(r.EveIdf, out _))
            .GroupBy(r => r.EveIdf)
            .ToDictionary(g => g.Key, g => g.First());

        var today = DateTime.Today;
        var candidates = new List<EventsDto>();

        foreach (var row in rowsById.Values)
        {
            if (cfg.OnlyPublished && row.EvePublicado is not ("1" or "true" or "True"))
            {
                continue;
            }

            var start = ParseDate(row.EveDataInicio);
            var end = ParseDate(row.EveDataTermino) ?? start;
            if (cfg.OnlyUpcoming && end is not null && end.Value.Date < today)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.EveTitulo))
            {
                continue;
            }

            candidates.Add(new EventsDto
            {
                Id = row.EveIdf,
                Name = row.EveTitulo,
                StartDate = start,
                EndDate = end,
                Lat = null,
                Long = null,
                Source = Provider
            });
        }

        if (cfg.EnableGeocoding)
        {
            var geocodeTargets = candidates.Take(Math.Max(0, cfg.GeocodeMax)).ToList();
            var coordsById = new Dictionary<string, (double? Lat, double? Long)>();

            foreach (var item in geocodeTargets)
            {
                if (!rowsById.TryGetValue(item.Id, out var row))
                {
                    continue;
                }

                var address = BuildAddress(row);
                var coords = await geocodingService.GeocodeAsync(address, cancellationToken);
                coordsById[item.Id] = coords;
            }

            candidates = candidates.Select(e =>
            {
                if (!coordsById.TryGetValue(e.Id, out var coords))
                {
                    return e;
                }

                return new EventsDto
                {
                    Id = e.Id,
                    Name = e.Name,
                    StartDate = e.StartDate,
                    EndDate = e.EndDate,
                    Lat = coords.Lat,
                    Long = coords.Long,
                    Source = e.Source
                };
            }).ToList();
        }

        logger.LogInformation("Fetched {Count} Curitiba PMC events", candidates.Count);
        return candidates;
    }

    private async Task<string> ResolveLatestCsvUrlAsync(CuritibaPmcOptions cfg, CancellationToken cancellationToken)
    {
        var url =
            $"{cfg.ListUrl}?conjuntoDadoChave={Uri.EscapeDataString(cfg.DatasetKey)}&conjuntoDadoExtensao={Uri.EscapeDataString(cfg.FormatKey)}";

        using var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PmcDownloadResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty response from Curitiba open data list API.");

        if (!payload.Sucesso || string.IsNullOrWhiteSpace(payload.Tabela))
        {
            throw new InvalidOperationException("Curitiba open data list API did not return files.");
        }

        var match = CsvHrefRegex.Match(payload.Tabela);
        if (!match.Success)
        {
            throw new InvalidOperationException("Could not find latest Curitiba PMC CSV download URL.");
        }

        return match.Value;
    }

    private static string BuildAddress(PmcEventRow row)
    {
        var parts = new[]
        {
            row.EveEndereco,
            row.BaiDescricao,
            "Curitiba",
            "PR",
            "Brazil"
        }.Where(p => !string.IsNullOrWhiteSpace(p));

        return string.Join(", ", parts);
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith('-'))
        {
            return null;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
        {
            return new DateTimeOffset(dt);
        }

        return null;
    }

    private sealed class PmcDownloadResponse
    {
        [JsonPropertyName("sucesso")]
        public bool Sucesso { get; set; }

        [JsonPropertyName("tabela")]
        public string? Tabela { get; set; }
    }

    private sealed class PmcEventRow
    {
        [Name("EVE_IDF")]
        public string EveIdf { get; set; } = string.Empty;

        [Name("EVE_TITULO")]
        public string? EveTitulo { get; set; }

        [Name("EVE_PUBLICADO")]
        public string? EvePublicado { get; set; }

        [Name("EVE_DATA_INICIO")]
        public string? EveDataInicio { get; set; }

        [Name("EVE_DATA_TERMINO")]
        public string? EveDataTermino { get; set; }

        [Name("EVE_ENDERECO")]
        public string? EveEndereco { get; set; }

        [Name("BAI_DESCRICAO")]
        public string? BaiDescricao { get; set; }
    }
}
