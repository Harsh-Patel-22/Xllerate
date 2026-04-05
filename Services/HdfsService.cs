// Services/HdfsService.cs

using System.Text.Json;

namespace Xllerate.Services;

public class HdfsService(
    IHttpClientFactory httpFactory,
    IConfiguration config,
    ILogger<HdfsService> logger) {
    private readonly IHttpClientFactory _httpFactory = httpFactory;

    public async Task<string?> ReadOutputJsonAsync(string resumeId)
    {
        var hdfsBase = config["Hdfs:BaseUrl"]!;
        var outputPath = $"/xllerate/output/{resumeId}";

        try
        {
            // Step 1 — list all files in the output directory
            var partFiles = await ListPartFilesAsync(hdfsBase, outputPath);

            if (!partFiles.Any())
            {
                logger.LogWarning(
                    "No part files found in HDFS output directory " +
                    "for resume {ResumeId}", resumeId);
                return null;
            }

            logger.LogInformation(
                "Found {Count} part file(s) in HDFS output for resume {ResumeId}: {Files}",
                partFiles.Count, resumeId, string.Join(", ", partFiles));

            // Step 2 — read and concatenate all part files
            // Spark may split output across multiple partitions
            var allContent = new List<string>();

            foreach (var partPath in partFiles)
            {
                var content = await ReadHdfsFileAsync(hdfsBase, partPath);
                if (!string.IsNullOrWhiteSpace(content))
                    allContent.Add(content.Trim());
            }

            if (!allContent.Any())
            {
                logger.LogWarning(
                    "All part files were empty for resume {ResumeId}", resumeId);
                return null;
            }

            // Step 3 — merge lines from all part files into one JSON string
            // Each line in a Spark text output is one JSON record
            var merged = MergeSparkOutput(allContent);
            return merged;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to read HDFS output for resume {ResumeId}", resumeId);
            return null;
        }
    }
    
    // ── List all part-* files in the output directory ─────────────────────────

    private async Task<List<string>> ListPartFilesAsync(
        string hdfsBase, string outputPath)
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = true };
        using var client = new HttpClient(handler);

        var listUrl = $"{hdfsBase}/webhdfs/v1{outputPath}?op=LISTSTATUS";
        var response = await client.GetAsync(listUrl);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "HDFS LISTSTATUS failed for {Path}: {Status}",
                outputPath, response.StatusCode);
            return [];
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var partFiles = new List<string>();

        // WebHDFS LISTSTATUS returns:
        // { "FileStatuses": { "FileStatus": [ { "pathSuffix": "part-00000", "type": "FILE" } ] } }
        if (!doc.RootElement.TryGetProperty("FileStatuses", out var fileStatuses))
            return [];

        if (!fileStatuses.TryGetProperty("FileStatus", out var fileStatusArray))
            return [];

        foreach (var fileStatus in fileStatusArray.EnumerateArray())
        {
            // Skip directories and non-data files (_SUCCESS, .crc files etc.)
            var type = fileStatus.TryGetProperty("type", out var t)
                ? t.GetString() : "";

            var suffix = fileStatus.TryGetProperty("pathSuffix", out var s)
                ? s.GetString() ?? "" : "";

            if (type != "FILE")
                continue;

            // Only read actual Spark data files — skip metadata files
            if (suffix.StartsWith("_") || suffix.StartsWith("."))
                continue;

            // Must start with "part-" to be a Spark output partition
            if (!suffix.StartsWith("part-"))
                continue;

            partFiles.Add($"{outputPath}/{suffix}");
        }

        // Sort to ensure consistent ordering (part-00000 before part-00001 etc.)
        partFiles.Sort();
        return partFiles;
    }
    
    // ── Read a single file from HDFS via WebHDFS OPEN ─────────────────────────

    private async Task<string?> ReadHdfsFileAsync(string hdfsBase, string filePath)
    {
        try
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var client = new HttpClient(handler);

            // Step 1 — initiate OPEN, get datanode redirect
            var openUrl = $"{hdfsBase}/webhdfs/v1{filePath}?op=OPEN";
            var initResponse = await client.GetAsync(openUrl);

            if (initResponse.StatusCode != System.Net.HttpStatusCode.TemporaryRedirect)
            {
                logger.LogWarning(
                    "HDFS OPEN did not redirect for {FilePath}: {Status}",
                    filePath, initResponse.StatusCode);
                return null;
            }

            // Step 2 — rewrite datanode hostname to localhost
            var datanodeUrl = initResponse.Headers.Location!.ToString();
            var rewrittenUrl = RewriteDatanodeUrl(datanodeUrl);

            logger.LogDebug(
                "Reading HDFS file {FilePath} from datanode {Url}",
                filePath, rewrittenUrl);

            // Step 3 — read the file content
            var fileResponse = await client.GetAsync(rewrittenUrl);

            if (!fileResponse.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "HDFS file read failed for {FilePath}: {Status}",
                    filePath, fileResponse.StatusCode);
                return null;
            }

            return await fileResponse.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Exception reading HDFS file {FilePath}", filePath);
            return null;
        }
    }

    // ── Merge content from multiple Spark part files ───────────────────────────

    private string? MergeSparkOutput(List<string> partContents)
    {
        // Collect all non-empty lines across all part files
        // Each line written by Spark saveAsTextFile is one JSON record
        var allLines = partContents
            .SelectMany(content => content.Split('\n'))
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (!allLines.Any())
            return null;

        if (allLines.Count == 1)
        {
            // Single record — return as-is (most common case)
            return allLines[0];
        }

        // Multiple lines — try to merge JSON objects
        // This handles the case where Spark splits one record across partitions
        // or writes multiple records (one per partition)
        logger.LogInformation(
            "Merging {Count} JSON lines from Spark output", allLines.Count);

        return MergeJsonLines(allLines);
    }

    private string MergeJsonLines(List<string> jsonLines)
    {
        // Parse all valid JSON lines
        var validDocs = new List<JsonElement>();

        foreach (var line in jsonLines)
        {
            try
            {
                var doc = JsonDocument.Parse(line);
                validDocs.Add(doc.RootElement.Clone());
            }
            catch (JsonException)
            {
                logger.LogWarning(
                    "Skipping invalid JSON line: {Line}",
                    line.Length > 100 ? line[..100] + "..." : line);
            }
        }

        if (!validDocs.Any())
            return jsonLines[0];   // return raw if nothing parsed

        if (validDocs.Count == 1)
            return validDocs[0].GetRawText();

        // Merge multiple JSON objects — first record wins for scalar fields,
        // arrays are unioned
        var merged = new Dictionary<string, JsonElement>();

        foreach (var doc in validDocs)
        {
            foreach (var prop in doc.EnumerateObject())
            {
                if (!merged.ContainsKey(prop.Name))
                {
                    // First occurrence — take it
                    merged[prop.Name] = prop.Value.Clone();
                }
                else if (prop.Value.ValueKind == JsonValueKind.Array
                      && merged[prop.Name].ValueKind == JsonValueKind.Array)
                {
                    // Arrays — union them (e.g. skills from different partitions)
                    var existing = merged[prop.Name]
                        .EnumerateArray()
                        .Select(e => e.GetRawText())
                        .ToHashSet();

                    var combined = merged[prop.Name]
                        .EnumerateArray()
                        .Select(e => e.Clone())
                        .ToList();

                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        if (!existing.Contains(item.GetRawText()))
                            combined.Add(item.Clone());
                    }

                    // Rebuild the array as a JsonElement
                    var arrayJson = "[" +
                        string.Join(",", combined.Select(e => e.GetRawText())) +
                        "]";
                    merged[prop.Name] = JsonDocument.Parse(arrayJson)
                        .RootElement.Clone();
                }
                // For scalar fields — first record wins, skip subsequent
            }
        }

        // Serialize merged result back to JSON string
        var resultJson = "{" + string.Join(",",
            merged.Select(kv =>
                $"{JsonSerializer.Serialize(kv.Key)}:{kv.Value.GetRawText()}"))
            + "}";

        return resultJson;
    }
    private static string RewriteDatanodeUrl(string originalUrl)
    {
        var uri = new Uri(originalUrl);
        return originalUrl.Replace(
            $"{uri.Scheme}://{uri.Host}:{uri.Port}",
            $"http://localhost:{uri.Port}");
    }
}