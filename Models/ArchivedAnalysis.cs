using System.ComponentModel.DataAnnotations.Schema;

namespace Xllerate.Models;

public class ArchivedAnalysis
{
    public Guid Id { get; set; }
    public Guid OriginalAnalysisId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ResumeId { get; set; }
    public Guid? JobRoleId { get; set; }
    public string? DesiredRole { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal MatchPercentage { get; set; }

    public string? ModelVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessedAt { get; set; }
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;

    // Full analysis payload as JSON — preserves everything without FK dependencies
    public string? SnapshotJson { get; set; }
}