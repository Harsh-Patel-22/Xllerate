// Services/JobScraping/JobNormalizationService.cs
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xllerate.Data;
using Xllerate.Enums;
using Xllerate.Models;
using Xllerate.Models.DTOs.Jobs;

namespace Xllerate.Services.JobScraping;

public class JobNormalizationService(
    ApplicationDbContext db,
    ILogger<JobNormalizationService> logger) {
    // Seniority keywords for classification
    private static readonly Dictionary<string, Seniority> SeniorityMap = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ["intern"]         = Seniority.Intern,
        ["internship"]     = Seniority.Intern,
        ["trainee"]        = Seniority.Intern,
        ["fresher"]      = Seniority.Intern,
        ["graduate"]     = Seniority.Intern,
        ["junior"]         = Seniority.Junior,
        ["associate"]      = Seniority.Junior,
        ["entry"]          = Seniority.Junior,
        ["entry level"]  = Seniority.Junior,
        ["entry-level"]  = Seniority.Junior,
        ["jr."]          = Seniority.Junior,
        ["mid"]            = Seniority.Mid,
        ["intermediate"]   = Seniority.Mid,
        ["ii"]           = Seniority.Mid,
        ["senior"]         = Seniority.Senior,
        ["sr."]            = Seniority.Senior,
        ["sr "]          = Seniority.Senior,
        [" i,"]          = Seniority.Senior,
        ["lead"]           = Seniority.Staff,
        ["staff"]          = Seniority.Staff,
        ["principal"]      = Seniority.Principal,
        ["architect"]      = Seniority.Principal,
        ["director"]       = Seniority.Principal,
        ["vp"]           = Seniority.Principal,
        ["head of"]      = Seniority.Principal,
    };

    // Known tech skills for extraction from JD text
    private static readonly HashSet<string> TechSkillKeywords = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "python", "java", "javascript", "typescript", "c#", "c++", "go",
        "rust", "kotlin", "swift", "ruby", "php", "scala", "react",
        "angular", "vue", "next.js", "node.js", "express", "django",
        "flask", "fastapi", "spring", "asp.net", "graphql", "rest",
        "machine learning", "deep learning", "nlp", "computer vision",
        "tensorflow", "pytorch", "keras", "scikit-learn", "pandas",
        "numpy", "spark", "hadoop", "kafka", "airflow", "aws", "azure",
        "gcp", "docker", "kubernetes", "terraform", "jenkins", "git",
        "sql", "mysql", "postgresql", "mongodb", "redis", "elasticsearch",
        "sql server", "oracle", "cassandra", "dynamodb", "agile", "scrum",
        "linux", "bash", "html", "css", "tailwind", "bootstrap",
        "data structures", "algorithms", "system design", "microservices",
        "ci/cd", "devops", "git", "github", "gitlab"
    };

    public async Task<(int added, int updated, int companiesAdded)>
        NormalizeAndPersistAsync(List<JobScrapedDto> jobs)
    {
        var added          = 0;
        var updated        = 0;
        var companiesAdded = 0;

        foreach (var job in jobs)
        {
            if (string.IsNullOrWhiteSpace(job.Title) ||
                string.IsNullOrWhiteSpace(job.CompanyName))
                continue;

            try
            {
                // Detect remote from all available fields — not just source flag
                var isRemote = DetectRemote(job);

// Extract skills now — used both for JobRoleSkills and RawRequirements
                var skillMatches = ExtractSkillsFromText(job.Description, job.Title);
                var skillNames   = skillMatches.Select(s => s.name).ToList();
                
                // Upsert company
                var (company, isNewCompany) =
                    await UpsertCompanyAsync(job.CompanyName);

                if (isNewCompany) companiesAdded++;

                // Normalize title and detect seniority
                var normalizedTitle = NormalizeTitle(job.Title);
                var seniority       = DetectSeniority(job.Title);

                // Deduplicate by company + normalized title + location
                var existing = await db.JobRoles
                    .FirstOrDefaultAsync(jr =>
                        jr.CompanyId      == company.Id &&
                        jr.NormalizedTitle == normalizedTitle &&
                        jr.Location       == job.Location);

                if (existing is not null)
                {
                    // Refresh the scrape timestamp and update raw data
                    existing.ScrapedAt       = DateTime.UtcNow;
                    existing.IsRemote        = isRemote;
                    existing.RequirementsRaw = BuildRawData(job, skillNames);
                    await db.SaveChangesAsync();
                    updated++;
                    continue;
                }

                // Create new JobRole
                var jobRole = new JobRole
                {
                    Id             = Guid.NewGuid(),
                    CompanyId      = company.Id,
                    Title          = job.Title.Trim(),
                    NormalizedTitle = normalizedTitle,
                    Seniority      = seniority,
                    Location       = job.Location,
                    IsRemote       = isRemote,
                    IsActive       = true,
                    SourceUrl      = job.SourceUrl,
                    ScrapedAt      = DateTime.UtcNow,
                    RequirementsRaw = BuildRawData(job, skillNames)
                };

                db.JobRoles.Add(jobRole);
                await db.SaveChangesAsync();

                // Extract and link required skills from JD text
                if (!string.IsNullOrWhiteSpace(job.Description))
                {
                    await LinkSkillsAsync(
                        jobRole.Id, skillMatches);
                }
                
                await LinkSkillsAsync(jobRole.Id, skillMatches);

                added++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to normalize job: {Title} at {Company}",
                    job.Title, job.CompanyName);
            }
        }

        return (added, updated, companiesAdded);
    }

    // ── Company upsert ────────────────────────────────────────────────────────

    private async Task<(Company company, bool isNew)> UpsertCompanyAsync(
        string companyName)
    {
        var normalized = NormalizeCompanyName(companyName);

        var existing = await db.Companies
            .FirstOrDefaultAsync(c => c.NormalizedName == normalized);

        if (existing is not null)
            return (existing, false);

        var company = new Company
        {
            Id             = Guid.NewGuid(),
            Name           = companyName.Trim(),
            NormalizedName = normalized,
            CreatedAt      = DateTime.UtcNow
        };

        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return (company, true);
    }

    // ── Skill extraction from JD text ─────────────────────────────────────────

    // Replace ExtractAndLinkSkillsAsync entirely
    private async Task LinkSkillsAsync(
        Guid jobRoleId,
        List<(string name, string normalized, decimal weight)> skillMatches)
    {
        if (!skillMatches.Any()) return;

        foreach (var (name, normalized, weight) in skillMatches)
        {
            var skill = await db.Skills
                .FirstOrDefaultAsync(s => s.NormalizedName == normalized);

            if (skill is null)
            {
                skill = new Skill
                {
                    Id             = Guid.NewGuid(),
                    Name           = name,
                    NormalizedName = normalized,
                    IsVerified     = true
                };
                db.Skills.Add(skill);
                await db.SaveChangesAsync();
            }

            var linkExists = await db.JobRoleSkills
                .AnyAsync(jrs => jrs.JobRoleId == jobRoleId && jrs.SkillId == skill.Id);

            if (!linkExists)
            {
                db.JobRoleSkills.Add(new JobRoleSkill
                {
                    JobRoleId        = jobRoleId,
                    SkillId          = skill.Id,
                    IsRequired       = weight >= 0.75m,
                    ImportanceWeight = weight
                });
            }
        }

        await db.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string NormalizeTitle(string title) =>
        Regex.Replace(title.ToLower().Trim(),
            @"\s+", " ");

    private static string NormalizeCompanyName(string name) =>
        Regex.Replace(name.ToLower().Trim()
            .Replace("inc.", "")
            .Replace("ltd.", "")
            .Replace("llc", "")
            .Replace("pvt.", "")
            .Replace("private limited", "")
            .Replace("limited", ""),
            @"\s+", " ").Trim();

    private static Seniority DetectSeniority(string title)
    {
        var lower = " " + title.ToLower().Trim() + " ";
        foreach (var (keyword, seniority) in SeniorityMap)
            if (lower.Contains(" " + keyword + " ") || lower.Contains(keyword))
                return seniority;
        return Seniority.Mid;   // default assumption
    }

    private static JobRoleRawData BuildRawData(JobScrapedDto job, List<string> extractedSkills) =>
        new()
        {
            DescriptionText  = job.Description,
            RawRequirements  = extractedSkills,
            ScraperVersion   = "v1.0"
        };
    
    private static bool DetectRemote(JobScrapedDto job)
    {
        var combined = $"{job.Title} {job.Location} {job.Description}".ToLower();
        return combined.Contains("remote") ||
               combined.Contains("work from home") ||
               combined.Contains("wfh") ||
               combined.Contains("distributed");
    }
    
    private static List<(string name, string normalized, decimal weight)> ExtractSkillsFromText(
        string? description, string title)
    {
        var found = new List<(string name, string normalized, decimal weight)>();

        // Always scan the title first — gives high-weight signals even without a description
        var titleLower = title.ToLower();
        foreach (var keyword in TechSkillKeywords)
        {
            if (!titleLower.Contains(keyword.ToLower())) continue;
            found.Add((keyword, keyword.ToLower(), 0.95m)); // title mention = very high weight
        }

        if (string.IsNullOrWhiteSpace(description)) return found;

        var descLower = description.ToLower();
        var titleSkills = found.Select(f => f.normalized).ToHashSet();

        foreach (var keyword in TechSkillKeywords)
        {
            var normalized = keyword.ToLower();
            if (titleSkills.Contains(normalized)) continue; // already added from title

            var pattern = $@"\b{Regex.Escape(keyword)}\b";
            if (!Regex.IsMatch(descLower, pattern, RegexOptions.IgnoreCase)) continue;

            var firstIndex = descLower.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            var positionRatio = 1.0 - ((double)firstIndex / descLower.Length);
            var weight = (decimal)Math.Round(0.5 + (positionRatio * 0.45), 4); // max 0.95 from desc

            found.Add((keyword, normalized, weight));
        }

        return found;
    }
}