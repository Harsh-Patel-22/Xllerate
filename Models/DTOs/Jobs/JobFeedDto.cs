namespace Xllerate.Models.DTOs.Jobs;

public class JobFeedDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string CompanyInitial =>
        string.IsNullOrEmpty(Company) ? "" : Company[0].ToString();     
    public string? CompanyColor { get; set; } = "#E5E7EB";   
    public string? Location { get; set; }
    public string? JobUrl { get; set; }
    public bool Remote { get; set; }
    public string? Salary { get; set; }
    public string? Experience { get; set; } = null;     
    public List<string> Skills { get; set; } = [];      
    public bool IsSaved { get; set; }
    public List<string> MissingSkills { get; set; } = []; 
    public int MatchScore { get; set; }                
    public string? ScrapedAt { get; set; }               
}