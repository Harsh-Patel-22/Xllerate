using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Xllerate.Models.MongoDB;

public class RoleProfileDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string RoleKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public int JobsSampled { get; set; }
    public DateTime LastUpdated { get; set; }
    public List<RoleProfileSkillDocument> Skills { get; set; } = [];
}

public class RoleProfileSkillDocument
{
    public string SkillName { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public Guid? SkillId { get; set; }

    public double ProbabilisticScore { get; set; }
    public double FrequencyRatio { get; set; }
    public bool IsCore { get; set; }
    public double Confidence { get; set; }
}