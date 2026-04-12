using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xllerate.Data;
using Xllerate.Models;

namespace Xllerate.Services;

public class RoleBucketDiscoveryService(
    ApplicationDbContext db,
    ILogger<RoleBucketDiscoveryService> logger)
{
    // How many top titles to consider for dynamic bucket creation
    private const int TopTitlesLimit = 100;

    // A title must appear this many times to be worth bucketing
    private const int MinTitleFrequency = 2;

    // Words that are too generic to anchor a bucket
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "engineer", "developer", "analyst", "specialist", "consultant",
        "manager", "lead", "senior", "junior", "associate", "intern",
        "remote", "work", "india", "bangalore", "mumbai", "pune",
        "hyderabad", "chennai", "delhi", "noida", "gurugram", "at"
    };

    // Seed buckets — canonical role keys + anchor keywords
    // Keep this minimal — just enough to establish identity
    public static readonly Dictionary<string, (string DisplayName, string[] SeedKeywords)> SeedBuckets = new()
    {
        ["software_engineering"] = ("Software Engineer", new[]
        {
            "software engineer", "software developer", "swe",
            "backend developer", "backend engineer",
            "frontend developer", "frontend engineer",
            "full stack", "fullstack", "web developer"
        }),
        ["data_ml"] = ("Data / ML Engineer", new[]
        {
            "data engineer", "data scientist", "machine learning",
            "ml engineer", "ai engineer", "mlops", "data analyst"
        }),
        ["devops_cloud"] = ("DevOps / Cloud Engineer", new[]
        {
            "devops", "cloud engineer", "site reliability",
            "sre", "platform engineer", "infrastructure engineer"
        }),
        ["product_management"] = ("Product Manager", new[]
        {
            "product manager", "technical product manager", "tpm"
        }),
    };

    /// <summary>
    /// Seeds DB with static keywords, then discovers dynamic ones from scraped titles.
    /// Returns the full resolved bucket map for aggregation to use.
    /// </summary>
    public async Task<Dictionary<string, (string DisplayName, List<string> Keywords)>>
        ResolveBucketsAsync(CancellationToken ct = default)
    {
        await SeedStaticKeywordsAsync(ct);
        await DiscoverDynamicKeywordsAsync(ct);
        return await LoadBucketsFromDbAsync(ct);
    }

    // ── Step 1: ensure seed keywords exist in DB ──────────────────────────────
    private async Task SeedStaticKeywordsAsync(CancellationToken ct)
    {
        foreach (var (roleKey, (_, keywords)) in SeedBuckets)
        {
            foreach (var keyword in keywords)
            {
                var exists = await db.RoleKeywordMappings
                    .AnyAsync(m => m.RoleKey == roleKey && m.Keyword == keyword, ct);

                if (exists) continue;

                db.RoleKeywordMappings.Add(new RoleKeywordMapping
                {
                    Id        = Guid.NewGuid(),
                    RoleKey   = roleKey,
                    Keyword   = keyword,
                    IsSeeded  = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        await db.SaveChangesAsync(ct);
    }

    // ── Step 2: find frequent titles not yet claimed by any bucket ────────────
    private async Task DiscoverDynamicKeywordsAsync(CancellationToken ct)
    {
        // Get all existing keywords to avoid re-processing
        var existingKeywords = await db.RoleKeywordMappings
            .Select(m => m.Keyword)
            .ToHashSetAsync(ct);

        // Top frequent normalized titles from scraped jobs
        var topTitles = await db.JobRoles
            .GroupBy(j => j.NormalizedTitle)
            .Select(g => new { Title = g.Key, Count = g.Count() })
            .Where(g => g.Count >= MinTitleFrequency)
            .OrderByDescending(g => g.Count)
            .Take(TopTitlesLimit)
            .ToListAsync(ct);

        // Load all current seed keywords per bucket for similarity matching
        var allMappings = await db.RoleKeywordMappings.ToListAsync(ct);
        var bucketKeywords = allMappings
            .GroupBy(m => m.RoleKey)
            .ToDictionary(g => g.Key, g => g.Select(m => m.Keyword).ToList());

        foreach (var entry in topTitles)
        {
            var title = entry.Title;

            // Already claimed by a bucket
            if (existingKeywords.Any(kw => title.Contains(kw, StringComparison.OrdinalIgnoreCase)))
            {
                // Update observed count on the matching mapping
                var match = allMappings.FirstOrDefault(m =>
                    title.Contains(m.Keyword, StringComparison.OrdinalIgnoreCase));
                if (match != null) match.ObservedCount += entry.Count;
                continue;
            }

            // Try to assign to nearest seed bucket
            var assignedBucket = FindNearestBucket(title, bucketKeywords);

            if (assignedBucket != null)
            {
                // Merge into existing bucket
                logger.LogInformation(
                    "Dynamic: '{Title}' → merged into bucket '{Bucket}'", title, assignedBucket);

                db.RoleKeywordMappings.Add(new RoleKeywordMapping
                {
                    Id            = Guid.NewGuid(),
                    RoleKey       = assignedBucket,
                    Keyword       = title,
                    IsSeeded      = false,
                    ObservedCount = entry.Count,
                    CreatedAt     = DateTime.UtcNow
                });
                existingKeywords.Add(title);
            }
            else
            {
                // No existing bucket matches — create a new dynamic bucket
                var newRoleKey = GenerateRoleKey(title);

                // Avoid duplicate dynamic buckets for the same concept
                if (await db.RoleKeywordMappings.AnyAsync(m => m.RoleKey == newRoleKey, ct))
                    continue;

                var displayName = ToDisplayName(title);
                logger.LogInformation(
                    "Dynamic: '{Title}' → new bucket '{Key}' ({Display})", title, newRoleKey, displayName);

                db.RoleKeywordMappings.Add(new RoleKeywordMapping
                {
                    Id            = Guid.NewGuid(),
                    RoleKey       = newRoleKey,
                    Keyword       = title,
                    IsSeeded      = false,
                    ObservedCount = entry.Count,
                    CreatedAt     = DateTime.UtcNow
                });
                existingKeywords.Add(title);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    // ── Step 3: load full resolved bucket map from DB ─────────────────────────
    private async Task<Dictionary<string, (string DisplayName, List<string> Keywords)>>
        LoadBucketsFromDbAsync(CancellationToken ct)
    {
        var mappings = await db.RoleKeywordMappings.ToListAsync(ct);

        var result = new Dictionary<string, (string DisplayName, List<string> Keywords)>();

        foreach (var group in mappings.GroupBy(m => m.RoleKey))
        {
            var roleKey     = group.Key;
            var keywords    = group.Select(m => m.Keyword).ToList();

            // Display name: use seed bucket name if known, else derive from role key
            var displayName = SeedBuckets.TryGetValue(roleKey, out var seed)
                ? seed.DisplayName
                : ToDisplayName(roleKey.Replace("_", " "));

            result[roleKey] = (displayName, keywords);
        }

        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds the best matching existing bucket for a title using word overlap.
    /// Returns null if no bucket is similar enough.
    /// </summary>
    private static string? FindNearestBucket(
        string title,
        Dictionary<string, List<string>> bucketKeywords)
    {
        var titleWords = ExtractMeaningfulWords(title);
        if (titleWords.Count == 0) return null;

        var bestBucket = (string?)null;
        var bestScore  = 0;

        foreach (var (bucket, keywords) in bucketKeywords)
        {
            var bucketWords = keywords
                .SelectMany(ExtractMeaningfulWords)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Score = number of meaningful words shared between title and bucket
            var overlap = titleWords.Count(w => bucketWords.Contains(w));
            if (overlap > bestScore)
            {
                bestScore  = overlap;
                bestBucket = bucket;
            }
        }

        // Require at least 1 meaningful word overlap to claim membership
        return bestScore >= 1 ? bestBucket : null;
    }

    private static List<string> ExtractMeaningfulWords(string text) =>
        text.ToLower()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !StopWords.Contains(w))
            .ToList();

    private static string GenerateRoleKey(string title) =>
        System.Text.RegularExpressions.Regex
            .Replace(title.ToLower().Trim(), @"[^a-z0-9]+", "_")
            .Trim('_');

    private static string ToDisplayName(string input) =>
        System.Globalization.CultureInfo.CurrentCulture.TextInfo
            .ToTitleCase(input.Replace("_", " ").Trim());
}