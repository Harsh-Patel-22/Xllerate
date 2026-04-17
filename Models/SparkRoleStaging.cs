namespace Xllerate.Models;

public class SparkRoleStaging
{
    public Guid Id { get; set; }
    public string RoleKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Guid SkillId { get; set; }
    public decimal AggregatedWeight { get; set; }
    public decimal FrequencyRatio { get; set; }
    public decimal ProbabilisticScore { get; set; }
    public bool IsCore { get; set; }
    public int JobsSampled { get; set; }
    public decimal Confidence { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public bool IsConsumed { get; set; } = false;
}