namespace Patamar.Gateway.External.Infrastructure.Configuration;

public sealed class SymplaPublicOptions
{
    public const string SectionName = "SymplaPublic";

    public string SearchUrl { get; set; } = "https://www.sympla.com.br/api/v1/search";
    public string City { get; set; } = "Curitiba";
    public string State { get; set; } = "PR";
    public int PageSize { get; set; } = 50;
    public int MaxPages { get; set; } = 10;
}
