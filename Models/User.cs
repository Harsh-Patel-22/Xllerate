using Microsoft.AspNetCore.Identity;
using Xllerate.Models.Interfaces;

namespace Xllerate.Models;

public class User : IdentityUser<Guid>, ITimestamped
{
    public required string FullName { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // JSON column — UI preferences only
    public UserPreferences Preferences { get; set; } = new();

    // Navigation
    public ICollection<Resume> Resumes { get; set; } = [];
    public ICollection<ExternalProfile> ExternalProfiles { get; set; } = [];
    public ICollection<UserSkill> UserSkills { get; set; } = [];
    public ICollection<Certification> Certifications { get; set; } = [];
    public ICollection<Project> Projects { get; set; } = [];
    public ICollection<Analysis> Analyses { get; set; } = [];
    public ICollection<Roadmap> Roadmaps { get; set; } = [];
}

// Owned type — maps to JSON column
public class UserPreferences
{
    public string Theme { get; set; } = "system";
    public bool EmailNotifications { get; set; } = true;
    public string? TargetRegion { get; set; }
}