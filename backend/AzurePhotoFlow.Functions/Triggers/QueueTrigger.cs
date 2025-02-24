using Functions.Interfaces;
using Functions.Models;
using Microsoft.Azure.WebJobs;
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

        [FunctionName("ProcessMetadataQueueMessage")]
        public async Task Run(
            [QueueTrigger("metadataqueue", Connection = "AzureWebJobsStorage")] string queueItem,
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
            await _metadataProcessor.ProcessAsync(metadata, log);
        }
    }
