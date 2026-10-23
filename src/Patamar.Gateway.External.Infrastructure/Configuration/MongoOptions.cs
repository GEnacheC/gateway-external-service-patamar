namespace Patamar.Gateway.External.Infrastructure.Configuration;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "gateway";
    public string EventsCollectionName { get; set; } = "events";
}
