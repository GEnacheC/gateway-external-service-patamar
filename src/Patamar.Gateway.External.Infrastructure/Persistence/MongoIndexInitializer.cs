using Patamar.Gateway.External.Application.Configuration;
using Patamar.Gateway.External.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Patamar.Gateway.External.Infrastructure.Persistence;

/// <summary>
/// Ensures MongoDB collections and geo indexes exist as soon as the API process starts.
/// </summary>
public sealed class MongoIndexInitializer(
    IMongoClient client,
    IOptions<MongoOptions> mongoOptions,
    IOptions<RadarOptions> radarOptions,
    ILogger<MongoIndexInitializer> logger) : IHostedService
{
    private const int MaxAttempts = 30;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await EnsureIndexesAsync(cancellationToken);
                logger.LogInformation(
                    "MongoDB indexes ready on database '{Database}' (events + radar_areas).",
                    mongoOptions.Value.DatabaseName);
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && !cancellationToken.IsCancellationRequested)
            {
                lastError = ex;
                logger.LogWarning(
                    ex,
                    "MongoDB index setup failed (attempt {Attempt}/{MaxAttempts}). Retrying...",
                    attempt,
                    MaxAttempts);
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"Failed to create MongoDB indexes after {MaxAttempts} attempts.",
            lastError);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var mongo = mongoOptions.Value;
        var database = client.GetDatabase(mongo.DatabaseName);

        var events = database.GetCollection<EventDocument>(mongo.EventsCollectionName);
        await events.Indexes.CreateOneAsync(
            new CreateIndexModel<EventDocument>(
                Builders<EventDocument>.IndexKeys.Ascending(x => x.Source).Ascending(x => x.Id),
                new CreateIndexOptions { Unique = true, Name = "ux_source_id" }),
            cancellationToken: cancellationToken);
        await events.Indexes.CreateOneAsync(
            new CreateIndexModel<EventDocument>(
                Builders<EventDocument>.IndexKeys.Geo2DSphere(x => x.Location),
                new CreateIndexOptions { Name = "ix_location_2dsphere" }),
            cancellationToken: cancellationToken);

        var radarCollectionName = radarOptions.Value.RadarAreasCollectionName;
        var radarAreas = database.GetCollection<RadarAreaDocument>(radarCollectionName);
        await radarAreas.Indexes.CreateOneAsync(
            new CreateIndexModel<RadarAreaDocument>(
                Builders<RadarAreaDocument>.IndexKeys.Ascending(x => x.Id),
                new CreateIndexOptions { Unique = true, Name = "ux_radar_id" }),
            cancellationToken: cancellationToken);
        await radarAreas.Indexes.CreateOneAsync(
            new CreateIndexModel<RadarAreaDocument>(
                Builders<RadarAreaDocument>.IndexKeys.Geo2DSphere(x => x.Location),
                new CreateIndexOptions { Name = "ix_radar_location_2dsphere" }),
            cancellationToken: cancellationToken);
    }
}
