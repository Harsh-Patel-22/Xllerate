using System.ComponentModel.DataAnnotations.Schema;

namespace Xllerate.Models;

public class RoleProfile
{
    public Guid Id { get; set; }
    public string RoleKey { get; set; } = string.Empty;      // e.g. "software_engineering"
    public string DisplayName { get; set; } = string.Empty;  // e.g. "Software Engineer"
    public int JobsSampled { get; set; }                      // how many scraped jobs contributed
    public DateTime LastAggregatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<RoleProfileSkill> Skills { get; set; } = [];
    
    [Column(TypeName = "decimal(5,4)")]
    public decimal Confidence { get; set; }
}