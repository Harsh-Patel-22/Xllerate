using System.ComponentModel.DataAnnotations.Schema;
using Xllerate.Enums;

namespace Xllerate.Models;

public class SkillGap
{
    public Guid Id { get; set; }
    public Guid AnalysisId { get; set; }
    public Guid SkillId { get; set; }
    public GapType GapType { get; set; }
    public GapSeverity Severity { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal UserScore { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal RequiredScore { get; set; }

    public string? Recommendation { get; set; }

    public Analysis Analysis { get; set; } = null!;
    public Skill Skill { get; set; } = null!;
    public ICollection<RoadmapItem> RoadmapItems { get; set; } = [];
}