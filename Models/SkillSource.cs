using System.ComponentModel.DataAnnotations.Schema;
using Xllerate.Enums;

namespace Xllerate.Models;

public class SkillSource
{
    public Guid Id { get; set; }
    public Guid UserSkillId { get; set; }
    public SkillSourceType SourceType { get; set; }

    // Polymorphic — points to Resume.Id, Project.Id, Certification.Id, etc.
    public Guid? SourceEntityId { get; set; }

    // Human-readable evidence string for display
    public string? Evidence { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal Weight { get; set; }

    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    public UserSkill UserSkill { get; set; } = null!;
}