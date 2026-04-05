// Services/HdfsService.cs
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
            // Step 1 — list the output directory to find part-00000
            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var client = new HttpClient(handler);

            var listUrl = $"{hdfsBase}/webhdfs/v1{outputPath}" +
                          $"?op=LISTSTATUS";
            var listResponse = await client.GetAsync(listUrl);

            if (!listResponse.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "HDFS output not found for resume {ResumeId}", resumeId);
                return null;
            }

            // Step 2 — read part-00000 (Spark output file)
            var partPath = $"{outputPath}/part-00000";
            var openUrl = $"{hdfsBase}/webhdfs/v1{partPath}?op=OPEN";

            var openInit = await client.GetAsync(openUrl);
            if (openInit.StatusCode != System.Net.HttpStatusCode.TemporaryRedirect)
                return null;

            // Rewrite datanode hostname to localhost
            var datanodeUrl = openInit.Headers.Location!.ToString();
            var uri = new Uri(datanodeUrl);
            var rewrittenUrl = datanodeUrl.Replace(
                $"{uri.Scheme}://{uri.Host}:{uri.Port}",
                $"http://localhost:{uri.Port}");

            var fileResponse = await client.GetAsync(rewrittenUrl);
            if (!fileResponse.IsSuccessStatusCode)
                return null;

            return await fileResponse.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to read HDFS output for resume {ResumeId}", resumeId);
            return null;
        }
    }
}