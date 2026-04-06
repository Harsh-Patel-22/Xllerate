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
        ["junior"]         = Seniority.Junior,
        ["associate"]      = Seniority.Junior,
        ["entry"]          = Seniority.Junior,
        ["mid"]            = Seniority.Mid,
        ["intermediate"]   = Seniority.Mid,
        ["senior"]         = Seniority.Senior,
        ["sr."]            = Seniority.Senior,
        ["lead"]           = Seniority.Staff,
        ["staff"]          = Seniority.Staff,
        ["principal"]      = Seniority.Principal,
        ["architect"]      = Seniority.Principal,
        ["director"]       = Seniority.Principal,
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
                    existing.RequirementsRaw = BuildRawData(job);
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
                    IsRemote       = job.IsRemote,
                    IsActive       = true,
                    SourceUrl      = job.SourceUrl,
                    ScrapedAt      = DateTime.UtcNow,
                    RequirementsRaw = BuildRawData(job)
                };

                db.JobRoles.Add(jobRole);
                await db.SaveChangesAsync();

                // Extract and link required skills from JD text
                if (!string.IsNullOrWhiteSpace(job.Description))
                {
                    await ExtractAndLinkSkillsAsync(
                        jobRole.Id, job.Description);
                }

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

    private async Task ExtractAndLinkSkillsAsync(
        Guid jobRoleId, string description)
    {
        var descLower = description.ToLower();
        var found     = new List<(string name, string normalized, decimal weight)>();

        foreach (var keyword in TechSkillKeywords)
        {
            var pattern = $@"\b{Regex.Escape(keyword)}\b";
            if (!Regex.IsMatch(descLower, pattern,
                RegexOptions.IgnoreCase))
                continue;

            // Weight based on position in JD —
            // skills mentioned early carry more importance
            var firstIndex = descLower.IndexOf(
                keyword, StringComparison.OrdinalIgnoreCase);
            var positionRatio = 1.0 - ((double)firstIndex / descLower.Length);
            var weight = (decimal)Math.Round(
                0.5 + (positionRatio * 0.5), 4);

            found.Add((keyword, keyword.ToLower(), weight));
        }

        if (!found.Any()) return;

        foreach (var (name, normalized, weight) in found)
        {
            // Resolve canonical skill
            var skill = await db.Skills
                .FirstOrDefaultAsync(s => s.NormalizedName == normalized);

            if (skill is null)
            {
                skill = new Skill
                {
                    Id             = Guid.NewGuid(),
                    Name           = name,
                    NormalizedName = normalized,
                    IsVerified     = true   // from JD — treat as verified
                };
                db.Skills.Add(skill);
                await db.SaveChangesAsync();
            }

            // Check if link already exists
            var linkExists = await db.JobRoleSkills
                .AnyAsync(jrs =>
                    jrs.JobRoleId == jobRoleId &&
                    jrs.SkillId   == skill.Id);

            if (!linkExists)
            {
                db.JobRoleSkills.Add(new JobRoleSkill
                {
                    JobRoleId       = jobRoleId,
                    SkillId         = skill.Id,
                    IsRequired      = weight > 0.7m,
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
        var lower = title.ToLower();
        foreach (var (keyword, seniority) in SeniorityMap)
            if (lower.Contains(keyword))
                return seniority;
        return Seniority.Mid;   // default assumption
    }

    private static JobRoleRawData BuildRawData(JobScrapedDto job) =>
        new()
        {
            DescriptionText  = job.Description,
            RawRequirements  = [],
            ScraperVersion   = "v1.0"
        };
}