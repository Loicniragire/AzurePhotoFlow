using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Cosmos;
using Functions.Interfaces;
using Functions.Services;

namespace AzurePhotoFlow.Functions;

public class Program
{
    public static void Main()
    {
        try
        {
            DotNetEnv.Env.Load();
            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureServices((context, services) =>
                {
                    ConfigureServices(context, services);
                })
                .Build();

            host.Run();
        }
        catch (Exception ex)
        {
            // Critical error logging
            File.WriteAllText("/home/logs/startup_error.txt",
                $"[{DateTime.UtcNow:o}] FATAL ERROR: {ex}");
            throw;
        }
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // Move your service configuration here
        var cosmosConnectionString = context.Configuration["CosmosDBConnectionString"]
            ?? throw new InvalidOperationException("Missing CosmosDB connection string");

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
    }
}
