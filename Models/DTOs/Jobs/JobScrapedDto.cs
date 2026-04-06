namespace Xllerate.Models.DTOs.Jobs;

public class JobScrapedDto
{
    public string Title { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string? Location { get; set; }
    public bool IsRemote { get; set; }
    public string? Description { get; set; }
    public string? SourceUrl { get; set; }
    public string? SalaryMin { get; set; }
    public string? SalaryMax { get; set; }
    public string? EmploymentType { get; set; }
    public string? Seniority { get; set; }
    public string Source { get; set; } = "";
    public DateTime ScrapedAt { get; set; } = DateTime.UtcNow;
}