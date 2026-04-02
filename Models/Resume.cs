namespace Xllerate.Models;

public class Resume
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string StoragePath { get; set; }
    public string? OriginalFileName { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ParsedAt { get; set; }
    public ResumeParseResult? ParsedData { get; set; }
    public bool IsActive { get; set; } = true;

    public User User { get; set; } = null!;
    public ICollection<SkillSource> SkillSources { get; set; } = [];
}

// Models/Resume.cs — updated ResumeParseResult
public class ResumeParseResult
{
    public string? Summary { get; set; }
    public string? Objective { get; set; }
    public List<string> RawSkills { get; set; } = [];
    public List<ParsedEducation> Education { get; set; } = [];
    public List<ParsedExperience> WorkExperience { get; set; } = [];
    public List<string> RawCertifications { get; set; } = [];

    // Replaced Dictionary<string, string> with typed class
    public ExtractedLinks Links { get; set; } = new();
    public ParsedContactInfo? ContactInfo { get; set; }
    public ParsedMetadata Metadata { get; set; } = new();

    // Remove GeminiModelVersion from here — it now lives inside ParsedMetadata
}

public class ExtractedLinks
{
    public string? LinkedIn { get; set; }
    public string? GitHub { get; set; }
    public string? LeetCode { get; set; }
    public string? Portfolio { get; set; }
    public List<string> Other { get; set; } = [];
}

public class ParsedContactInfo
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
}

public class ParsedMetadata
{
    public string? GeminiModelVersion { get; set; }
    public string? PromptVersion { get; set; }
    public decimal ConfidenceScore { get; set; }
    public int TokensUsed { get; set; }
    public DateTime ParsedAt { get; set; } = DateTime.UtcNow;
    public List<string> ParseWarnings { get; set; } = [];
}

public class ParsedEducation
{
    public string? Institution { get; set; }
    public string? Degree { get; set; }
    public string? FieldOfStudy { get; set; }
    public string? Grade { get; set; }
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }
    public bool IsCurrentlyEnrolled { get; set; }
    public List<string> Achievements { get; set; } = [];
    public List<string> RelevantCoursework { get; set; } = [];
}

public class ParsedExperience
{
    public string? Company { get; set; }
    public string? Title { get; set; }
    public string? Location { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public List<string> Responsibilities { get; set; } = [];
    public List<string> TechnologiesUsed { get; set; } = [];
    public string? EmploymentType { get; set; }
}
