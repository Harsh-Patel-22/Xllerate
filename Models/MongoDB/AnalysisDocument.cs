using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Xllerate.Models.MongoDB;

public class AnalysisDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid AnalysisId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; set; }

    public string JobTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal MatchPercentage { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AnalysisType { get; set; } = string.Empty; // "Job" | "Role" | "Posting"
    public DateTime CreatedAt { get; set; }
    public List<SkillGapDocument> SkillGaps { get; set; } = [];
    public SkillSummaryDocument Summary { get; set; } = new();
    public AnalysisInsightDocument? InsightData { get; set; }
    public List<string> SoftSkillSuggestions { get; set; } = [];
}

public class SkillGapDocument
{
    public string SkillName { get; set; } = string.Empty;
    public string GapType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public decimal UserScore { get; set; }
    public decimal RequiredScore { get; set; }
    public string? Recommendation { get; set; }
}

public class SkillSummaryDocument
{
    public int TotalRequired { get; set; }
    public int Matched { get; set; }
    public int Missing { get; set; }
    public int Weak { get; set; }
    public int Unverified { get; set; }
}

public class AnalysisInsightDocument
{
    public string? Summary { get; set; }
    public string? WhyNotHired { get; set; }
    public List<string> Strengths { get; set; }
    public List<string> CriticalGaps { get; set; }
    public string? MarketContext { get; set; }
}