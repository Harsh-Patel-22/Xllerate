using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using Xllerate.Data;
using Xllerate.Models;
using Xllerate.Services;

namespace Xllerate.Services;

public class AnalysisArchiveJob(
    ApplicationDbContext db,
    MongoDbContext mongo,
    ILogger<AnalysisArchiveJob> logger)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);

        // Load active resume per user for exclusion
        var activeResumeIds = await db.Resumes
            .Where(r => r.IsActive)
            .Select(r => r.Id)
            .ToHashSetAsync(ct);

        var stale = await db.Analyses
            .Where(a =>
                !a.IsDeleted &&
                a.LastAccessedAt < cutoff &&
                (a.ResumeId == null || !activeResumeIds.Contains(a.ResumeId.Value)))
            .ToListAsync(ct);

        if (stale.Count == 0) return;

        logger.LogInformation("Archiving {Count} stale analyses.", stale.Count);

        foreach (var analysis in stale)
        {
            // Fetch MongoDB doc for snapshot
            var mongoDoc = await mongo.Analyses
                .Find(a => a.AnalysisId == analysis.Id)
                .FirstOrDefaultAsync(ct);

            var snapshot = mongoDoc != null
                ? JsonSerializer.Serialize(mongoDoc)
                : null;

            // Write to archive table
            db.ArchivedAnalyses.Add(new ArchivedAnalysis
            {
                Id                 = Guid.NewGuid(),
                OriginalAnalysisId = analysis.Id,
                UserId             = analysis.UserId,
                ResumeId           = analysis.ResumeId,
                JobRoleId          = analysis.JobRoleId,
                MatchPercentage    = analysis.MatchPercentage,
                ModelVersion       = analysis.ModelVersion,
                CreatedAt          = analysis.CreatedAt,
                LastAccessedAt     = analysis.LastAccessedAt,
                ArchivedAt         = DateTime.UtcNow,
                SnapshotJson       = snapshot
            });

            // Mark deleted in SQL
            analysis.IsDeleted  = true;
            analysis.DeletedAt  = DateTime.UtcNow;

            // Delete from MongoDB
            await mongo.Analyses.DeleteOneAsync(
                a => a.AnalysisId == analysis.Id, ct);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Archive complete — {Count} analyses archived.", stale.Count);
    }
}