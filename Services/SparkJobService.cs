// Services/SparkJobService.cs
namespace Xllerate.Services;

public class SparkJobService(IHttpClientFactory httpFactory, ILogger<SparkJobService> logger) {
    public async Task SubmitResumeParseJobAsync(Guid resumeId, string hdfsPath)
    {
        var client = httpFactory.CreateClient("spark");

        var payload = new
        {
            resumeId = resumeId.ToString(),
            hdfsPath = hdfsPath
        };

        var response = await client.PostAsJsonAsync("/run-job", payload);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            logger.LogError(
                "Spark job trigger failed for resume {ResumeId}: {Error}",
                resumeId, error);
            throw new Exception($"Failed to trigger Spark job: {error}");
        }

        logger.LogInformation(
            "Spark job submitted for resume {ResumeId} at {HdfsPath}",
            resumeId, hdfsPath);
    }
    
    public async Task SubmitRoleAggregationJobAsync()
    {
        var client   = httpFactory.CreateClient("spark");
        var response = await client.PostAsync("/aggregate-roles", null);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            logger.LogError("Role aggregation job trigger failed: {Error}", error);
            throw new Exception($"Failed to trigger role aggregation: {error}");
        }

        logger.LogInformation("Role aggregation Spark job submitted.");
    }
}