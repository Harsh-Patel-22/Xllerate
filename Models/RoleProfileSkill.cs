using System.ComponentModel.DataAnnotations.Schema;

namespace Xllerate.Models;

public class RoleProfileSkill
{
    public Guid RoleProfileId { get; set; }
    public Guid SkillId { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal AggregatedWeight { get; set; }   // avg ImportanceWeight across jobs

    [Column(TypeName = "decimal(5,4)")]
    public decimal FrequencyRatio { get; set; }     // appears in X% of jobs in this bucket

    public bool IsCore { get; set; }                // true if FrequencyRatio >= 0.5

    public RoleProfile RoleProfile { get; set; } = null!;
    public Skill Skill { get; set; } = null!;
    [Column(TypeName = "decimal(5,4)")]
    public decimal ProbabilisticScore { get; set; }  // final blended score
}