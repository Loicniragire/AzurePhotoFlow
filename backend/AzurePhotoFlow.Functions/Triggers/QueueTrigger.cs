using Functions.Interfaces;
using Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
namespace Functions.Triggers;

public class ProcessQueueMessage
{
    private readonly IMetadataProcessor _metadataProcessor;
    private readonly ILogger _logger;

    public ProcessQueueMessage(IMetadataProcessor metadataProcessor, ILoggerFactory loggerFactory)
    {
        _metadataProcessor = metadataProcessor;
        _logger = loggerFactory.CreateLogger<ProcessQueueMessage>();
    }

    [Function(nameof(MetadataBatchProcessor))]
    public async Task MetadataBatchProcessor(
        [QueueTrigger("image-metadata-queue", Connection = "AzureWebJobsStorage")] string[] queueItems)
    {

        foreach (var queueItem in queueItems)
        {
            try
            {
                // Deserialize the message into a metadata object.
                var metadata = JsonConvert.DeserializeObject<ImageMetadata>(queueItem);
                if (metadata == null)
                {
                    _logger.LogError("Failed to deserialize queue message.");
                    continue;
                }

                // Process the metadata using the injected service.
                await _metadataProcessor.ProcessAsync(metadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred processing the queue message.");
            }
        }
    }
}
