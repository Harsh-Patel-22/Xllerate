namespace Xllerate.Models;

public class Certification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public required string Issuer { get; set; }
    public string? CredentialId { get; set; }
    public string? VerificationUrl { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsVerified { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public ICollection<SkillSource> SkillSources { get; set; } = [];
}