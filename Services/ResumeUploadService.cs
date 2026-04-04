// Services/ResumeUploadService.cs

using Microsoft.EntityFrameworkCore;
using Xllerate.Data;
using Xllerate.Models;

namespace Xllerate.Services;

public class ResumeUploadService(
    IHttpClientFactory httpFactory,
    ApplicationDbContext db,
    IConfiguration config,
    ILogger<ResumeUploadService> logger) {
    // Allowed file types
    private static readonly string[] AllowedExtensions = [".pdf"];
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB

    public async Task<Resume> UploadAsync(Guid userId, IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new InvalidOperationException("Only PDF files are accepted.");

        if (file.Length == 0)
            throw new InvalidOperationException("File is empty.");

        if (file.Length > MaxFileSizeBytes)
            throw new InvalidOperationException("File exceeds 10MB limit.");

        // ── Build HDFS path ───────────────────────────────────────────────────
        var resumeId = Guid.NewGuid();
        var fileName = $"{userId}_{resumeId}.pdf";
        var hdfsResumesPath = config["Hdfs:ResumesPath"]!; // /xllerate/resumes
        var hdfsPath = $"{hdfsResumesPath}/{fileName}";

        // ── Write to HDFS via WebHDFS REST API ────────────────────────────────
        await WriteToHdfsAsync(hdfsPath, file);

        // ── Deactivate previous active resumes for this user ──────────────────
        var previousResumes = db.Resumes
            .Where(r => r.UserId == userId && r.IsActive);
        await previousResumes.ForEachAsync(r => r.IsActive = false);

        // ── Save Resume record to SQL Server ──────────────────────────────────
        var resume = new Resume
        {
            Id = resumeId,
            UserId = userId,
            StoragePath = hdfsPath,
            OriginalFileName = file.FileName,
            FileSizeBytes = file.Length,
            UploadedAt = DateTime.UtcNow,
            IsActive = true,
            ParsedAt = null   // will be set after Spark completes
        };

        db.Resumes.Add(resume);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Resume {ResumeId} uploaded to HDFS at {HdfsPath} for user {UserId}",
            resumeId, hdfsPath, userId);

        return resume;
    }

    private async Task WriteToHdfsAsync(string hdfsPath, IFormFile file)
    {
        var client = httpFactory.CreateClient("hdfs");
        var hdfsBase = config["Hdfs:BaseUrl"]!;

        // Step 1 — initiate CREATE, get redirect URL pointing to datanode
        var createUrl = $"/webhdfs/v1{hdfsPath}?op=CREATE&overwrite=false";
        var initRequest = new HttpRequestMessage(HttpMethod.Put, createUrl);

        // Do NOT follow redirect automatically — we need the Location header
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var redirectClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(hdfsBase)
        };

        var initResponse = await redirectClient.SendAsync(initRequest);

        if (initResponse.StatusCode != System.Net.HttpStatusCode.TemporaryRedirect)
        {
            var body = await initResponse.Content.ReadAsStringAsync();
            throw new Exception($"HDFS CREATE init failed: {initResponse.StatusCode} — {body}");
        }

        var datanodeUrl = initResponse.Headers.Location
            ?? throw new Exception("HDFS did not return a datanode redirect URL.");

        // Step 2 — stream file bytes to the datanode URL
        using var stream = file.OpenReadStream();
        var content = new StreamContent(stream);
        content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        var uploadResponse = await redirectClient.PutAsync(datanodeUrl, content);

        if (!uploadResponse.IsSuccessStatusCode)
        {
            var body = await uploadResponse.Content.ReadAsStringAsync();
            throw new Exception($"HDFS file upload failed: {uploadResponse.StatusCode} — {body}");
        }
    }
}