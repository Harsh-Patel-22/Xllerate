namespace Xllerate.Models.DTOs.Enrichment;

public class LeetCodeSubmissionDto
{
    public string Title { get; set; } = "";
    public string TitleSlug { get; set; } = "";
    public string StatusDisplay { get; set; } = "";
    public string Lang { get; set; } = "";
    public long Timestamp { get; set; }
}

public class LeetCodeBadgeDto
{
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
}

public class LeetCodeEnrichmentResult
{
    public string Username { get; set; } = "";
    public int TotalSolved { get; set; }
    public int EasySolved { get; set; }
    public int MediumSolved { get; set; }
    public int HardSolved { get; set; }
    public int TotalQuestions { get; set; }
    public int EasyTotal { get; set; }
    public int MediumTotal { get; set; }
    public int HardTotal { get; set; }
    public double AcceptanceRate { get; set; }
    public int Ranking { get; set; }
    public int ContributionPoints { get; set; }
    public int ReputationPoints { get; set; }
    public List<LeetCodeSubmissionDto> RecentSubmissions { get; set; } = [];
    public List<LeetCodeBadgeDto> Badges { get; set; } = [];
    public List<string> LanguagesUsed { get; set; } = [];
}