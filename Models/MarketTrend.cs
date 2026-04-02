using System.ComponentModel.DataAnnotations.Schema;

namespace Xllerate.Models;

public class MarketTrend
{
    public Guid Id { get; set; }
    public Guid SkillId { get; set; }
    public string Region { get; set; } = "Global";
    public string? JobRoleTitle { get; set; }
    public int MentionCount { get; set; }
    public int TotalJobsAnalyzed { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal MentionPercentage { get; set; }

    [Column(TypeName = "decimal(7,4)")]
    public decimal? GrowthRate { get; set; }  // MoM % change

    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;

    public Skill Skill { get; set; } = null!;
}