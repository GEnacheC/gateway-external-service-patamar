using Patamar.Gateway.External.Domain.Events;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Patamar.Gateway.External.Infrastructure.Persistence;

internal sealed class EventDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? MongoId { get; set; }

    public required string Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public double? Lat { get; set; }
    public double? Long { get; set; }
    public required string Source { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>GeoJSON Point [longitude, latitude] for 2dsphere queries.</summary>
    public GeoJsonPoint? Location { get; set; }

    public static EventDocument FromDto(EventsDto dto)
    {
        GeoJsonPoint? location = null;
        if (dto.Lat is not null && dto.Long is not null)
        {
            location = GeoJsonPoint.Create(dto.Long.Value, dto.Lat.Value);
        }

        return new EventDocument
        {
            Id = dto.Id,
            Name = dto.Name,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Lat = dto.Lat,
            Long = dto.Long,
            Source = dto.Source,
            UpdatedAt = DateTimeOffset.UtcNow,
            Location = location
        };
    }

    public EventsDto ToDto() => new()
    {
        Id = Id,
        Name = Name,
        StartDate = StartDate,
        EndDate = EndDate,
        Lat = Lat,
        Long = Long,
        Source = Source
    };
}

internal sealed class GeoJsonPoint
{
    [BsonElement("type")]
    public string Type { get; set; } = "Point";

    /// <summary>[longitude, latitude]</summary>
    [BsonElement("coordinates")]
    public double[] Coordinates { get; set; } = [];

    public static GeoJsonPoint Create(double longitude, double latitude) => new()
    {
        Type = "Point",
        Coordinates = [longitude, latitude]
    };
}
