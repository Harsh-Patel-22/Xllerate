using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Xllerate.Models.MongoDB;


public class RoadmapDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid AnalysisId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PeriodLabel { get; set; } = string.Empty;
    public int TimeframeDays { get; set; }
    public int EstimatedTotalHours { get; set; }
    public bool IsActive { get; set; }
    public int RoadmapCount { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<RoadmapItemDocument> Items { get; set; } = [];
}

public class RoadmapItemDocument
{
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    public string ItemType { get; set; } = string.Empty;
    public string? ActivityType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int EstimatedHours { get; set; }
    public int OrderIndex { get; set; }
    public int PeriodIndex { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? SkillName { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? SkillId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? SkillGapId { get; set; }
}