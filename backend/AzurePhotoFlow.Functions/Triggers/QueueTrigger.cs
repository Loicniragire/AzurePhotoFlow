using Azure.Storage.Queues;
using Functions.Interfaces;
using Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
namespace Functions.Triggers;

public class ProcessQueueMessage
{
    private readonly IMetadataProcessor _metadataProcessor;
	private readonly QueueClient _queueClient;
	private readonly ILogger<ProcessQueueMessage> _logger;

    // Dependencies are injected via the constructor.
    public ProcessQueueMessage(IMetadataProcessor metadataProcessor, QueueClient queueClient, ILogger<ProcessQueueMessage> logger)
	{
		_metadataProcessor = metadataProcessor;
		_queueClient = queueClient;
		_logger = logger;
    }

    [Function("ProcessMetadataQueueMessage")]
    public async Task Run(
        [QueueTrigger("image-metadata-queue", Connection = "AzureWebJobsStorage")] string queueItem)
    {
        _logger.LogInformation($"Processing queue item: {queueItem}");

		var properties = await _queueClient.GetPropertiesAsync();
		_logger.LogInformation($"Queue length: {properties.Value.ApproximateMessagesCount}");

        // Deserialize the message into a metadata object.
        var metadata = JsonConvert.DeserializeObject<ImageMetadata>(queueItem);
        if (metadata == null)
        {
            _logger.LogError("Failed to deserialize queue message.");
            return;
        }

        // Process the metadata using the injected service.
        await _metadataProcessor.ProcessAsync(metadata);
    }
}
