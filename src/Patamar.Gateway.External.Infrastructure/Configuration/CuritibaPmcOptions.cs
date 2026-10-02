namespace Patamar.Gateway.External.Infrastructure.Configuration;

public sealed class CuritibaPmcOptions
{
    public const string SectionName = "CuritibaPmc";

    public string ListUrl { get; set; } =
        "https://dadosabertos.curitiba.pr.gov.br/ConjuntoDado/DownloadArquivos/";

    public string DatasetKey { get; set; } = "471e0496-6f97-4c20-a32f-76af61372584";
    public string FormatKey { get; set; } = "377f4e23-0e4f-4f11-954f-ae06ba689558";
    public bool OnlyPublished { get; set; } = true;
    public bool OnlyUpcoming { get; set; } = false;
    public bool EnableGeocoding { get; set; } = true;
    public int GeocodeMax { get; set; } = 80;
}
