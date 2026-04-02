namespace Xllerate.Models;

public class Skill
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }  // lowercase, trimmed
    public string? Category { get; set; }   // "Frontend", "Backend", "ML", "DevOps"
    public string? Domain { get; set; }     // "Web", "Data Science", "Cloud"
    public bool IsVerified { get; set; } = true;  // false for auto-extracted unknowns
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserSkill> UserSkills { get; set; } = [];
    public ICollection<JobRoleSkill> JobRoleSkills { get; set; } = [];
    public ICollection<SkillSource> SkillSources { get; set; } = [];
    public ICollection<MarketTrend> MarketTrends { get; set; } = [];
    public ICollection<SkillGap> SkillGaps { get; set; } = [];
}