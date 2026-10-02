using Patamar.Gateway.External.Application.Abstractions;
using Patamar.Gateway.External.Application.Configuration;
using Patamar.Gateway.External.Domain.Events;
using Patamar.Gateway.External.Domain.Geo;
using Patamar.Gateway.External.Domain.Radar;
using Patamar.Gateway.External.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Patamar.Gateway.External.Infrastructure.Persistence;

public sealed class MongoEventRepository : IEventRepository
{
    private readonly IMongoCollection<EventDocument> _collection;

    public MongoEventRepository(IMongoClient client, IOptions<MongoOptions> options)
    {
        var mongo = options.Value;
        var database = client.GetDatabase(mongo.DatabaseName);
        _collection = database.GetCollection<EventDocument>(mongo.EventsCollectionName);

        _collection.Indexes.CreateOne(
            new CreateIndexModel<EventDocument>(
                Builders<EventDocument>.IndexKeys.Ascending(x => x.Source).Ascending(x => x.Id),
                new CreateIndexOptions { Unique = true, Name = "ux_source_id" }));

        _collection.Indexes.CreateOne(
            new CreateIndexModel<EventDocument>(
                Builders<EventDocument>.IndexKeys.Geo2DSphere(x => x.Location),
                new CreateIndexOptions { Name = "ix_location_2dsphere" }));
    }

    public async Task UpsertManyAsync(
        IEnumerable<EventsDto> events,
        CancellationToken cancellationToken = default)
    {
        var models = new List<WriteModel<EventDocument>>();

        foreach (var dto in events)
        {
            var document = EventDocument.FromDto(dto);
            var filter = Builders<EventDocument>.Filter.And(
                Builders<EventDocument>.Filter.Eq(x => x.Source, document.Source),
                Builders<EventDocument>.Filter.Eq(x => x.Id, document.Id));

            var update = Builders<EventDocument>.Update
                .Set(x => x.Name, document.Name)
                .Set(x => x.StartDate, document.StartDate)
                .Set(x => x.EndDate, document.EndDate)
                .Set(x => x.Lat, document.Lat)
                .Set(x => x.Long, document.Long)
                .Set(x => x.Location, document.Location)
                .Set(x => x.UpdatedAt, document.UpdatedAt)
                .SetOnInsert(x => x.Id, document.Id)
                .SetOnInsert(x => x.Source, document.Source);

            models.Add(new UpdateOneModel<EventDocument>(filter, update) { IsUpsert = true });
        }

        if (models.Count == 0)
        {
            return;
        }

        await _collection.BulkWriteAsync(models, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<EventsDto>> GetNearbyAsync(
        double lat,
        double lng,
        double radiusKm,
        CancellationToken cancellationToken = default)
    {
        var maxDistanceMeters = radiusKm * 1000.0;
        var filter = new BsonDocument(
            "Location",
            new BsonDocument(
                "$nearSphere",
                new BsonDocument
                {
                    {
                        "$geometry",
                        new BsonDocument
                        {
                            { "type", "Point" },
                            { "coordinates", new BsonArray { lng, lat } }
                        }
                    },
                    { "$maxDistance", maxDistanceMeters }
                }));

        var documents = await _collection
            .Find(filter)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;

        // Upcoming first (nearest StartDate to now), then past (most recent first), nulls last.
        return documents
            .OrderBy(d => d.StartDate is null ? 2 : d.StartDate >= now ? 0 : 1)
            .ThenBy(d => d.StartDate is null
                ? TimeSpan.MaxValue
                : d.StartDate >= now
                    ? d.StartDate.Value - now
                    : now - d.StartDate.Value)
            .Select(d => d.ToDto())
            .ToList();
    }
}

public sealed class MongoRadarAreaRepository : IRadarAreaRepository
{
    private readonly IMongoCollection<RadarAreaDocument> _collection;

    public MongoRadarAreaRepository(
        IMongoClient client,
        IOptions<MongoOptions> mongoOptions,
        IOptions<RadarOptions> radarOptions)
    {
        var database = client.GetDatabase(mongoOptions.Value.DatabaseName);
        _collection = database.GetCollection<RadarAreaDocument>(radarOptions.Value.RadarAreasCollectionName);

        _collection.Indexes.CreateOne(
            new CreateIndexModel<RadarAreaDocument>(
                Builders<RadarAreaDocument>.IndexKeys.Ascending(x => x.Id),
                new CreateIndexOptions { Unique = true, Name = "ux_radar_id" }));

        _collection.Indexes.CreateOne(
            new CreateIndexModel<RadarAreaDocument>(
                Builders<RadarAreaDocument>.IndexKeys.Geo2DSphere(x => x.Location),
                new CreateIndexOptions { Name = "ix_radar_location_2dsphere" }));
    }

    public async Task<RadarArea?> FindCoveringAreaAsync(
        double lat,
        double lng,
        DateTimeOffset minSyncedAt,
        CancellationToken cancellationToken = default)
    {
        var candidates = await _collection
            .Find(x => x.LastSyncedAt >= minSyncedAt)
            .ToListAsync(cancellationToken);

        return candidates
            .Select(c => c.ToDomain())
            .FirstOrDefault(area =>
                GeoDistance.Kilometers(area.Lat, area.Long, lat, lng) <= area.RadiusKm);
    }

    public async Task UpsertAreaAsync(RadarArea area, CancellationToken cancellationToken = default)
    {
        var document = RadarAreaDocument.FromDomain(area);
        var filter = Builders<RadarAreaDocument>.Filter.Eq(x => x.Id, document.Id);
        var update = Builders<RadarAreaDocument>.Update
            .Set(x => x.Lat, document.Lat)
            .Set(x => x.Long, document.Long)
            .Set(x => x.RadiusKm, document.RadiusKm)
            .Set(x => x.LastSyncedAt, document.LastSyncedAt)
            .Set(x => x.Providers, document.Providers)
            .Set(x => x.Location, document.Location)
            .SetOnInsert(x => x.Id, document.Id);

        await _collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }
}
