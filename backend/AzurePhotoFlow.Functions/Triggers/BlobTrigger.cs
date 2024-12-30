using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

public class BlobTrigger
{
    [FunctionName("ProcessUploadedImage")]
    public async Task Run(
        [BlobTrigger("images/{name}", Connection = "AzureWebJobsStorage")] Stream blobStream,
        string name,
        ILogger log)
    {
        log.LogInformation($"Processing blob: {name}");

        // TODO: Add metadata extraction and tagging logic here
    }
}

