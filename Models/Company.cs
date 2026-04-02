namespace Xllerate.Models;

public class Company
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public string? Industry { get; set; }
    public string? Website { get; set; }
    public string? LogoUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<JobRole> JobRoles { get; set; } = [];
}