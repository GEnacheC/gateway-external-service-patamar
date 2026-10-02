namespace Patamar.Gateway.External.Infrastructure.Configuration;

public sealed class TicketmasterOptions
{
    public const string SectionName = "Ticketmaster";

    public string BaseUrl { get; set; } = "https://app.ticketmaster.com/discovery/v2/";
    public string ApiKey { get; set; } = string.Empty;
    public double Lat { get; set; } = -25.4284;
    public double Long { get; set; } = -49.2733;
    public int RadiusKm { get; set; } = 50;
    public int PageSize { get; set; } = 100;
    public int MaxPages { get; set; } = 5;
}
