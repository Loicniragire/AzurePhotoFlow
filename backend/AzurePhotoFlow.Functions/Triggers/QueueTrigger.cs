using Functions.Interfaces;
using Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
namespace Functions.Triggers;

public class ProcessQueueMessage
{
    private readonly IMetadataProcessor _metadataProcessor;

    // Dependencies are injected via the constructor.
    public ProcessQueueMessage(IMetadataProcessor metadataProcessor)
    {
        _metadataProcessor = metadataProcessor;
    }

    [Function("ProcessMetadataQueueMessage")]
    public async Task Run(
        [QueueTrigger("image-metadata-queue", Connection = "AzureWebJobsStorage")] string queueItem,
        ILogger log)
    {
        log.LogInformation($"Processing queue item: {queueItem}");

        // Deserialize the message into a metadata object.
        var metadata = JsonConvert.DeserializeObject<ImageMetadata>(queueItem);
        if (metadata == null)
        {
            log.LogError("Failed to deserialize queue message.");
            return;
        }

        // Process the metadata using the injected service.
        await _metadataProcessor.ProcessAsync(metadata);
    }
}
