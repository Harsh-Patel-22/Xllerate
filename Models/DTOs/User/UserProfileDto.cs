using Xllerate.Enums;

namespace Xllerate.Models.DTOs.User;

public class UserProfileDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Plan { get; set; } = null;         
    public int ProfileCompletion { get; set; }
    public ResumeDto? Resume { get; set; }
}

public class ResumeDto
{
    public string Id { get; set; } = string.Empty;
    public string? Filename { get; set; }
    public string? UploadedAt { get; set; }
    public SyncStatus Status { get; set; } = SyncStatus.Pending; 
    public List<ResumeSkillDto> Skills { get; set; } = [];
    public List<ResumeEducationDto> Education { get; set; } = [];
    public List<ResumeExperienceDto> Experience { get; set; } = [];
    public List<ResumeLinkDto> Links { get; set; } = [];
}

public class ResumeSkillDto
{
    public string Name { get; set; } = string.Empty;
    public int Confidence { get; set; }                
    public List<SkillSourceType> Sources { get; set; } = [];   
}

public class ResumeEducationDto
{
    public string? Degree { get; set; }
    public string? Institution { get; set; }
    public string? Period { get; set; }
    public string? GradeValue { get; set; }   // "8.5", "3.8", "A+", "85"
    public string? GradeType { get; set; }    // "gpa_10" | "gpa_4" | "percentage" | "letter"
}

public class ResumeExperienceDto
{
    public string? Role { get; set; }
    public string? Company { get; set; }
    public string? Period { get; set; }
}

public class ResumeLinkDto
{
    public Platform Type { get; set; } = Platform.GitHub;  
    public string? Url { get; set; }
    public bool Connected { get; set; }
}

public class LatestAnalysisDto
{
    public string Id { get; set; } = string.Empty;
    public string ResumeId { get; set; } = string.Empty;
    public string TargetRole { get; set; } = string.Empty;
    public decimal MatchScore { get; set; }
    public string MatchLabel { get; set; } = string.Empty; // "competitive" | "moderate" | "weak"
    public List<ResumeSkillDto> Strengths { get; set; } = [];
    public List<ResumeSkillDto> WeakSkills { get; set; } = [];
    public List<ResumeSkillDto> MissingSkills { get; set; } = [];
    public List<GapSummaryDto> Gaps { get; set; } = [];
    public List<InsightDto>? Insights { get; set; } = null;
    public string CreatedAt { get; set; } = string.Empty;
}

public class GapSummaryDto
{
    public string Skill { get; set; } = string.Empty;
    public GapType GapType { get; set; } = GapType.Missing;    // "missing" | "weak" | "unverified"
    public GapSeverity Severity { get; set; } = GapSeverity.Critical;   // "critical" | "important" | "nice"
}

public class InsightDto
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class DashboardDto
{
    public ProfileSummaryDto Profile { get; set; } = new();
    public GitHubMetricsDto? GitHub { get; set; }
    public LeetCodeMetricsDto? LeetCode { get; set; }
    public LatestAnalysisSummaryDto? LatestAnalysis { get; set; }
    public List<TechNewsDto> News { get; set; } = [];
    public RadarChartDto RadarChart { get; set; } = new();
}

public class ProfileSummaryDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int ProfileCompletion { get; set; }
    public string? DesiredRole { get; set; }
    public string? AvatarUrl { get; set; }
}

public class GitHubMetricsDto
{
    public string Username { get; set; } = string.Empty;
    public string ProfileUrl { get; set; } = string.Empty;
    public int PublicRepos { get; set; }
    public int TotalStars { get; set; }
    public int TotalForks { get; set; }
    public int Followers { get; set; }
    public int TotalCommitsEstimate { get; set; }
    public List<string> TopLanguages { get; set; } = [];
    public DateTime? LastSyncedAt { get; set; }
}

public class LeetCodeMetricsDto
{
    public string Username { get; set; } = string.Empty;
    public string ProfileUrl { get; set; } = string.Empty;
    public int TotalSolved { get; set; }
    public int EasySolved { get; set; }
    public int MediumSolved { get; set; }
    public int HardSolved { get; set; }
    public int TotalQuestions { get; set; }
    public double AcceptanceRate { get; set; }
    public int Ranking { get; set; }
    public List<string> LanguagesUsed { get; set; } = [];
    public List<string> Badges { get; set; } = [];
    public DateTime? LastSyncedAt { get; set; }
}

public class LatestAnalysisSummaryDto
{
    public string AnalysisId { get; set; } = string.Empty;
    public string TargetRole { get; set; } = string.Empty;
    public decimal MatchPercentage { get; set; }
    public string MatchLabel { get; set; } = string.Empty;
    public int TotalSkills { get; set; }
    public int MatchedSkills { get; set; }
    public int MissingSkills { get; set; }
    public int WeakSkills { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TechNewsDto
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Source { get; set; }
    public string? PublishedAt { get; set; }
}

public class RadarChartDto
{
    public List<RadarAxisDto> Axes { get; set; } = [];
}

public class RadarAxisDto
{
    public string Category { get; set; } = string.Empty;   // "Frontend", "Backend", etc.
    public int UserScore { get; set; }                      // 0–100
    public int RequiredScore { get; set; }                  // 0–100 from role profile
    public bool IsPrimary { get; set; }
}
