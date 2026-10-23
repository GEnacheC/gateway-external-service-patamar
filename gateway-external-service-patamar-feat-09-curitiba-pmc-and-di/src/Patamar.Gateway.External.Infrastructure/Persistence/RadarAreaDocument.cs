using Patamar.Gateway.External.Domain.Radar;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Patamar.Gateway.External.Infrastructure.Persistence;

internal sealed class RadarAreaDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? MongoId { get; set; }

    public required string Id { get; set; }
    public required double Lat { get; set; }
    public required double Long { get; set; }
    public required double RadiusKm { get; set; }
    public required DateTimeOffset LastSyncedAt { get; set; }
    public List<string> Providers { get; set; } = [];
    public GeoJsonPoint Location { get; set; } = GeoJsonPoint.Create(0, 0);

    public static RadarAreaDocument FromDomain(RadarArea area) => new()
    {
        Id = area.Id,
        Lat = area.Lat,
        Long = area.Long,
        RadiusKm = area.RadiusKm,
        LastSyncedAt = area.LastSyncedAt,
        Providers = area.Providers.ToList(),
        Location = GeoJsonPoint.Create(area.Long, area.Lat)
    };

    public RadarArea ToDomain() => new()
    {
        Id = Id,
        Lat = Lat,
        Long = Long,
        RadiusKm = RadiusKm,
        LastSyncedAt = LastSyncedAt,
        Providers = Providers
    };
}
