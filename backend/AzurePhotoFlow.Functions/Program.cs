using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Cosmos;
using Functions.Interfaces;
using Functions.Services;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Azure.Core.Serialization;
using System.Text.Json.Serialization;

namespace AzurePhotoFlow.Functions;

public class Program
{
    public static void Main()
    {
        // Load environment variables first
        DotNetEnv.Env.Load();
        
        var host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults(worker => 
            {
                // Configure JSON serializer options if needed
                worker.Serializer = new JsonObjectSerializer(
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    });
            })
            .ConfigureServices((hostContext, services) =>
            {
                // Get configuration from host context
                var configuration = hostContext.Configuration;

                // Get Cosmos DB connection string
                var cosmosConnectionString = configuration.GetValue<string>("CosmosDBConnectionString") 
                    ?? throw new InvalidOperationException(
                        "Missing 'CosmosDBConnectionString' in configuration. " +
                        "Check your application settings or environment variables.");

                // Register CosmosClient with configuration
                services.AddSingleton<CosmosClient>(serviceProvider => 
                    new CosmosClient(
                        connectionString: cosmosConnectionString,
                        new CosmosClientOptions
                        {
                            SerializerOptions = new CosmosSerializationOptions
                            {
                                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                            },
                        }));

                // Register application services
                services.AddSingleton<IMetadataProcessor, ImageMetadataProcessor>();
            })
            .Build();

        host.Run();
    }
}
