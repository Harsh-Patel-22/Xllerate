using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Xllerate.Data;
using Xllerate.Models;
using Xllerate.Models.MongoDB;

namespace Xllerate.Services;

public class RoleProfileAggregationService(
    MongoDbContext mongo,
    SparkJobService sparkJobService,
    ILogger<RoleProfileAggregationService> logger) {
    // private const int MinJobsRequired = 1;
    // private const decimal MinFrequencyThreshold = 0.02m;
    //
    // private const decimal BayesianPrior = 0.15m;
    // private const int PriorStrength = 5;

    public async Task AggregateAsync(CancellationToken ct = default) {
        logger.LogInformation("Role profile aggregation started — consuming Spark staging.");

        var startTime = DateTime.UtcNow.AddSeconds(-5); // Small buffer for clock skew
        
        // Trigger Spark job and wait — or call this after Spark signals completion
        await sparkJobService.SubmitRoleAggregationJobAsync();

        // Poll until staging has fresh unconsumed rows (max 5 min)
        var deadline = DateTime.UtcNow.AddMinutes(5);
        // Use a threshold from just before the Spark job was triggered
        var lastUpdatedThreshold = startTime; 
        
        while (DateTime.UtcNow < deadline) {
            ct.ThrowIfCancellationRequested();

            var hasFreshData = await mongo.RoleProfiles
                .Find(r => r.LastUpdated >= lastUpdatedThreshold)
                .AnyAsync(ct);

            if (hasFreshData) break;

            await Task.Delay(5000, ct);
        }

        // Read all freshly written profiles from Mongo
        var profiles = await mongo.RoleProfiles
            .Find(r => r.LastUpdated >= lastUpdatedThreshold)
            .ToListAsync(ct);

        if (profiles.Count == 0) {
            logger.LogWarning("No fresh role profiles found in MongoDB after Spark job. Aggregation skipped.");
            return;
        }

        logger.LogInformation("Role profile aggregation complete. {Count} profiles synced.", profiles.Count);
    }

    private async Task<List<RoleProfileDocument>> ReadFromMongoAsync(string roleKey)
    {
        return await mongo.RoleProfiles
            .Find(r => r.RoleKey == roleKey)
            .ToListAsync();
    }

    
    // private async Task AggregateRoleAsync(
    //     string roleKey, string displayName, string[] keywords, CancellationToken ct)
    // {
    //     // ── Find all scraped jobs matching this bucket ────────────────────────
    //     // Load normalized titles + their skills in one query
    //     var matchingJobs = await db.JobRoles
    //         .Include(j => j.RequiredSkills)
    //             .ThenInclude(jrs => jrs.Skill)
    //         .Where(j => keywords.Any(kw =>
    //             j.NormalizedTitle.Contains(kw)))
    //         .ToListAsync(ct);
    //
    //     if (matchingJobs.Count < MinJobsRequired)
    //     {
    //         logger.LogWarning(
    //             "Role '{RoleKey}': only {Count} jobs found (min {Min}). Skipping.",
    //             roleKey, matchingJobs.Count, MinJobsRequired);
    //         return;
    //     }
    //
    //     var totalJobs = matchingJobs.Count;
    //     logger.LogInformation("Role '{RoleKey}': aggregating from {Count} jobs.", roleKey, totalJobs);
    //
    //     // ── Aggregate skills across all jobs in bucket ────────────────────────
    //     // skillId → (totalWeight, jobCount)
    //     var skillAccumulator = new Dictionary<Guid, (string Name, string Normalized, decimal TotalWeight, int JobCount)>();
    //
    //     foreach (var job in matchingJobs)
    //     {
    //         // Track which skills appeared in THIS job (avoid double-counting per job)
    //         var seenInThisJob = new HashSet<Guid>();
    //
    //         foreach (var jrs in job.RequiredSkills)
    //         {
    //             if (!seenInThisJob.Add(jrs.SkillId)) continue;
    //
    //             if (skillAccumulator.TryGetValue(jrs.SkillId, out var existing))
    //             {
    //                 skillAccumulator[jrs.SkillId] = existing with
    //                 {
    //                     TotalWeight = existing.TotalWeight + jrs.ImportanceWeight,
    //                     JobCount    = existing.JobCount + 1
    //                 };
    //             }
    //             else
    //             {
    //                 skillAccumulator[jrs.SkillId] = (
    //                     jrs.Skill.Name,
    //                     jrs.Skill.NormalizedName,
    //                     jrs.ImportanceWeight,
    //                     1
    //                 );
    //             }
    //         }
    //     }
    //
    //     var profileConfidence = (decimal)(totalJobs / (totalJobs + 10.0));
    //
    //     // ── Filter + compute probabilistic weights ────────────────────────────────
    //     var aggregatedSkills = skillAccumulator
    //         .Select(kvp =>
    //         {
    //             var (name, normalized, totalWeight, jobCount) = kvp.Value;
    //
    //             var rawFrequency = (decimal)jobCount / totalJobs;
    //
    //             // Bayesian-smoothed frequency:
    //             // Formula: (jobCount + prior * priorStrength) / (totalJobs + priorStrength)
    //             var smoothedFrequency = (jobCount + BayesianPrior * PriorStrength)
    //                                     / (totalJobs + PriorStrength);
    //
    //             // Average importance weight across jobs where skill appeared
    //             var avgWeight = totalWeight / jobCount;
    //
    //             var probabilisticScore = smoothedFrequency * avgWeight * profileConfidence;
    //
    //             return new
    //             {
    //                 SkillId              = kvp.Key,
    //                 FrequencyRatio       = rawFrequency,        // store raw for display
    //                 SmoothedFrequency    = smoothedFrequency,
    //                 AggregatedWeight     = avgWeight,
    //                 ProbabilisticScore   = Math.Round(probabilisticScore, 4),
    //             };
    //         })
    //         .Where(s => s.FrequencyRatio >= MinFrequencyThreshold)  // only exclude true noise
    //         .OrderByDescending(s => s.ProbabilisticScore)
    //         .ToList();
    //
    //     if (aggregatedSkills.Count == 0)
    //     {
    //         logger.LogWarning("Role '{RoleKey}': no skills passed frequency threshold.", roleKey);
    //         return;
    //     }
    //
    //     // ── Upsert RoleProfile ────────────────────────────────────────────────
    //     var profile = await db.RoleProfiles
    //         .Include(rp => rp.Skills)
    //         .FirstOrDefaultAsync(rp => rp.RoleKey == roleKey, ct);
    //
    //     if (profile == null)
    //     {
    //         profile = new RoleProfile
    //         {
    //             Id          = Guid.NewGuid(),
    //             RoleKey     = roleKey,
    //             DisplayName = displayName,
    //             CreatedAt   = DateTime.UtcNow,
    //         };
    //         db.RoleProfiles.Add(profile);
    //     }
    //
    //     profile.JobsSampled      = totalJobs;
    //     profile.Confidence       = Math.Round(profileConfidence, 4);  // ← new
    //     profile.LastAggregatedAt = DateTime.UtcNow;
    //     profile.UpdatedAt        = DateTime.UtcNow;
    //
    //     var newSkills = aggregatedSkills.Select(s => new RoleProfileSkill
    //     {
    //         RoleProfileId      = profile.Id,
    //         SkillId            = s.SkillId,
    //         AggregatedWeight   = Math.Round(s.AggregatedWeight, 4),
    //         FrequencyRatio     = Math.Round(s.FrequencyRatio, 4),
    //         ProbabilisticScore = s.ProbabilisticScore,             // ← new
    //         IsCore             = s.SmoothedFrequency >= 0.40m,     // ← lowered from 0.50
    //     }).ToList();
    //
    //     db.RoleProfileSkills.AddRange(newSkills);
    //     await db.SaveChangesAsync(ct);
    //
    //     logger.LogInformation(
    //         "Role '{RoleKey}': profile saved. {Count} skills ({Core} core). Confidence={Conf}, Jobs={Jobs}.",
    //         roleKey, newSkills.Count, newSkills.Count(s => s.IsCore),
    //         profileConfidence, totalJobs);
    // }
}