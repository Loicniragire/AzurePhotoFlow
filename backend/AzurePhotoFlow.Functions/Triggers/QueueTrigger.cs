using AzurePhotoFlow.POCO.QueueModels;
using Functions.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

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
        [QueueTrigger("image-metadata-queue", Connection = "AzureWebJobsStorage")] ImageMetadata[] queueItems)
    {

		_logger.LogInformation($"{nameof(MetadataBatchProcessor)} Processing {queueItems.Count()} queue items.");
        foreach (var queueItem in queueItems)
        {
            try
            {
                // Process the metadata using the injected service.
                await _metadataProcessor.ProcessAsync(queueItem);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred processing the queue message.");
            }
        }
    }
}
