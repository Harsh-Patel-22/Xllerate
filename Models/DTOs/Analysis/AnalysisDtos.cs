namespace Xllerate.Models.DTOs.Analysis;

public class TriggerAnalysisRequest
{
    public Guid JobRoleId { get; set; }
}

public class AnalysisResultDto
{
    public Guid AnalysisId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal MatchPercentage { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<SkillGapDto> SkillGaps { get; set; } = [];
    public SkillSummaryDto Summary { get; set; } = new();
}

public class SkillGapDto
{
    public string SkillName { get; set; } = string.Empty;
    public string GapType { get; set; } = string.Empty;      // Missing | Weak | Unverified | Outdated
    public string Severity { get; set; } = string.Empty;     // Critical | Important | NiceToHave
    public decimal UserScore { get; set; }                    // 0 if missing
    public decimal RequiredScore { get; set; }
    public decimal ScoreGap => RequiredScore - UserScore;
    public string? Recommendation { get; set; }
}

public class SkillSummaryDto
{
    public int TotalRequired { get; set; }
    public int Matched { get; set; }
    public int Missing { get; set; }
    public int Weak { get; set; }                             // have skill but score too low
    public int Unverified { get; set; }                       // claimed but no evidence
}