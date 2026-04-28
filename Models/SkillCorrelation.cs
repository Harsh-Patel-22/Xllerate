using System.ComponentModel.DataAnnotations.Schema;

namespace Xllerate.Models;

public class SkillCorrelation
{
    public Guid Id { get; set; }
    public Guid SkillAId { get; set; }
    public Guid SkillBId { get; set; }
    public string RoleKey { get; set; } = string.Empty;

    [Column(TypeName = "decimal(5,4)")]
    public decimal Strength { get; set; }

    public CorrelationSource Source { get; set; }
    public int? SampleSize { get; set; }
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;

    public Skill SkillA { get; set; } = null!;
    public Skill SkillB { get; set; } = null!;
}

public enum CorrelationSource { Static, Gemini }