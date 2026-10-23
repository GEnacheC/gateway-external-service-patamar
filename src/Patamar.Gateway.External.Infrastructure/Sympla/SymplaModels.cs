using System.Text.Json.Serialization;

namespace Patamar.Gateway.External.Infrastructure.Sympla;

internal sealed class SymplaEventsResponse
{
    [JsonPropertyName("data")]
    public List<SymplaEventData>? Data { get; set; }

    [JsonPropertyName("pagination")]
    public SymplaPagination? Pagination { get; set; }
}

internal sealed class SymplaPagination
{
    [JsonPropertyName("has_next")]
    public bool HasNext { get; set; }

    [JsonPropertyName("page_num")]
    public int PageNum { get; set; }

    [JsonPropertyName("page_size")]
    public int PageSize { get; set; }

    [JsonPropertyName("total_page")]
    public int TotalPage { get; set; }
}

internal sealed class SymplaEventData
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("start_date")]
    public string? StartDate { get; set; }

    [JsonPropertyName("end_date")]
    public string? EndDate { get; set; }

    [JsonPropertyName("address")]
    public SymplaAddress? Address { get; set; }
}

internal sealed class SymplaAddress
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("lat")]
    public string? Lat { get; set; }

    [JsonPropertyName("lon")]
    public string? Lon { get; set; }

    [JsonPropertyName("long")]
    public string? Long { get; set; }
}
