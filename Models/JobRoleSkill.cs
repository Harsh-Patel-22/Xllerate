using System.ComponentModel.DataAnnotations.Schema;

namespace Xllerate.Models;

public class JobRoleSkill
{
    public Guid JobRoleId { get; set; }
    public Guid SkillId { get; set; }
    public bool IsRequired { get; set; } = true;

    [Column(TypeName = "decimal(5,4)")]
    public decimal ImportanceWeight { get; set; } = 0.5m;

    [Column(TypeName = "decimal(5,4)")]
    public decimal? FrequencyInMarket { get; set; }

    public JobRole JobRole { get; set; } = null!;
    public Skill Skill { get; set; } = null!;
}