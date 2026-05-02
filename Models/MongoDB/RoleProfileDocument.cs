using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Xllerate.Models.MongoDB;

public class RoleProfileDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = string.Empty; 
    
    [BsonElement("roleKey")]
    public string RoleKey { get; set; } = string.Empty;
    [BsonElement("displayName")]
    public string DisplayName { get; set; } = string.Empty;
    [BsonElement("jobsSampled")]
    public int JobsSampled { get; set; }
    [BsonElement("confidence")]
    public double Confidence { get; set; }
    [BsonElement("lastUpdated")]
    public DateTime LastUpdated { get; set; }
    [BsonElement("skills")]
    public List<RoleProfileSkillDocument> Skills { get; set; } = [];
}

public class RoleProfileSkillDocument
{
    [BsonElement("skillName")]
    public string SkillName { get; set; } = string.Empty;
    [BsonElement("skillId")]
    [BsonRepresentation(BsonType.String)]
    public Guid? SkillId { get; set; }
    [BsonElement("probabilisticScore")]
    public double ProbabilisticScore { get; set; }
    [BsonElement("aggregatedWeight")]
    public double AggregatedWeight { get; set; }
    [BsonElement("frequencyRatio")]
    public double FrequencyRatio { get; set; }
    [BsonElement("isCore")]
    public bool IsCore { get; set; }
    [BsonElement("confidence")]
    public double Confidence { get; set; }
}