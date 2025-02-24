using Functions.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Functions.Services;

public class ImageMetadataProcessor
{
	private const string DatabaseName = "loicportraits-cosmosdb";
	private const string ContainerName = "MetadataContainer";
    private readonly Container _container;
    private readonly ILogger<ImageMetadataProcessor> _logger;

    public ImageMetadataProcessor(CosmosClient cosmosClient, ILogger<ImageMetadataProcessor> logger)
    {
        _container = cosmosClient.GetDatabase(DatabaseName).GetContainer(ContainerName);
        _logger = logger;
    }

    public async Task ProcessAsync(ImageMetadata metadata)
    {
        try
        {
            await _container.CreateItemAsync(metadata, new PartitionKey(metadata.Id));
            _logger.LogInformation("Inserted metadata into CosmosDB.");
        }
        catch (CosmosException ex)
        {
            _logger.LogError($"CosmosDB operation failed: {ex.StatusCode} - {ex.Message}");
        }
    }
}

