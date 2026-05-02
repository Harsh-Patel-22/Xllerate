using System.ComponentModel.DataAnnotations.Schema;
using Xllerate.Enums;

namespace Xllerate.Models;

public class Analysis
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? JobRoleId { get; set; }
    public Guid? ResumeId { get; set; }
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DesiredRoleSnapshot { get; set; }


    [Column(TypeName = "decimal(5,2)")]
    public decimal MatchPercentage { get; set; }

    public AnalysisStatus Status { get; set; } = AnalysisStatus.Processing;
    public string? ModelVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Structured narrative from Gemini — read as a unit for display
    public AnalysisInsight? InsightData { get; set; }
    public int RoadmapCount { get; set; } = 0;

    public Resume? Resume { get; set; }
    public User User { get; set; } = null!;
    [ForeignKey(nameof(JobRoleId))]
    public JobRole? JobRole { get; set; } = null!;
    public ICollection<SkillGap> SkillGaps { get; set; } = [];
    public Roadmap? Roadmap { get; set; }
}

public class AnalysisInsight
{
    public string? Summary { get; set; }
    public string? WhyNotHired { get; set; }
    public List<string> Strengths { get; set; } = [];
    public List<string> CriticalGaps { get; set; } = [];
    public string? MarketContext { get; set; }
    public string? GeminiRawResponse { get; set; }  // for debugging
}