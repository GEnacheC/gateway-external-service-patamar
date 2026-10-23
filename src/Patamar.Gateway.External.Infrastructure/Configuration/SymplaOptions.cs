namespace Patamar.Gateway.External.Infrastructure.Configuration;

public sealed class SymplaOptions
{
    public const string SectionName = "Sympla";

    public string BaseUrl { get; set; } = "https://api.sympla.com.br/public/v1.5.1";
    public string Token { get; set; } = string.Empty;
}
