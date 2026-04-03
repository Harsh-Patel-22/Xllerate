namespace Xllerate.Models.DTOs.Resume;
using Xllerate.Enums;

public class ResumeUploadResponse
{
    public Guid ResumeId { get; set; }
    public AnalysisStatus Status { get; set; } = AnalysisStatus.Processing;
    public string Message { get; set; } = "";
    public DateTime UploadedAt { get; set; }
}

public class ResumeStatusResponse
{
    public Guid ResumeId { get; set; }
    public AnalysisStatus Status { get; set; }   // "processing" | "complete" | "failed"
    public DateTime UploadedAt { get; set; }
    public DateTime? ParsedAt { get; set; }
    public List<ExtractedSkillDto> Skills { get; set; } = [];
}

public class ExtractedSkillDto
{
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public decimal ConfidenceScore { get; set; }
    public string VerificationStatus { get; set; } = "";
    public string Source { get; set; } = "";
}