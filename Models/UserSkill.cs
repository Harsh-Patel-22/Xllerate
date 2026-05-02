using System.ComponentModel.DataAnnotations.Schema;
using Xllerate.Enums;

namespace Xllerate.Models;

public class UserSkill
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SkillId { get; set; }
    public Guid? ResumeId { get; set; }
    public Resume? Resume { get; set; }

    // Computed by backend scoring engine — never set directly by user
    [Column(TypeName = "decimal(5,4)")]
    public decimal ConfidenceScore { get; set; }

    public SkillVerificationStatus VerificationStatus { get; set; }
        = SkillVerificationStatus.Claimed;

    [Column(TypeName = "decimal(4,1)")]
    public decimal? YearsOfExperience { get; set; }

    public DateTime? LastDemonstratedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // JSON — lightweight metrics snapshot, not queryable fields
    public SkillMetadata? Metadata { get; set; }

    public User User { get; set; } = null!;
    public Skill Skill { get; set; } = null!;
    public ICollection<SkillSource> Sources { get; set; } = [];
}

public class SkillMetadata
{
    public int? GithubRepoCount { get; set; }
    public int? GithubCommitCount { get; set; }
    public int? LeetcodeTagSolves { get; set; }
    public List<string>? SupportingRepoUrls { get; set; }
}