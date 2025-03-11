using Functions.Interfaces;
using Functions.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Functions.Services;

public class ImageMetadataProcessor : IMetadataProcessor
{
    private const string DatabaseName = "loicportraits-cosmosdb";
    private const string ContainerName = "MetadataContainer";
    private readonly Container _container;
    private readonly ILogger<ImageMetadataProcessor> _logger;

    public ImageMetadataProcessor(CosmosClient cosmosClient, ILogger<ImageMetadataProcessor> logger)
    {
        _logger = logger;
        var database = cosmosClient.GetDatabase(DatabaseName);
        var containerResponse = database.CreateContainerIfNotExistsAsync(ContainerName, "/id")
                                          .GetAwaiter().GetResult();
        _container = containerResponse.Container;

        _logger.LogInformation($"{nameof(ImageMetadataProcessor)} initialized. Container '{ContainerName}' is ready in database '{DatabaseName}'.");
    }
    public async Task ProcessAsync(ImageMetadata metadata)
    {
        _logger.LogInformation($"{nameof(ImageMetadataProcessor)} processing request");
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

