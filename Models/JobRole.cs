using Xllerate.Enums;

namespace Xllerate.Models;

public class JobRole
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public required string Title { get; set; }
    public required string NormalizedTitle { get; set; }
    public Seniority Seniority { get; set; }
    public string? Location { get; set; }
    public bool IsRemote { get; set; }
    public bool IsActive { get; set; } = true;
    public string? SourceUrl { get; set; }
    public DateTime ScrapedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }

    // Raw JD text and any structured data from the scraper
    public JobRoleRawData? RequirementsRaw { get; set; }

    public Company Company { get; set; } = null!;
    public ICollection<JobRoleSkill> RequiredSkills { get; set; } = [];
    public ICollection<Analysis> Analyses { get; set; } = [];
}

public class JobRoleRawData
{
    public string? DescriptionText { get; set; }
    public List<string> RawRequirements { get; set; } = [];
    public string? ScraperVersion { get; set; }
}