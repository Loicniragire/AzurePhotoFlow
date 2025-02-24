using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Cosmos;
using Functions.Interfaces;
using Functions.Services;

namespace AzurePhotoFlow.Functions;
public class Program
{
    public static void Main()
    {
        DotNetEnv.Env.Load();
        var host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureServices((hostContext, services) =>
            {
                // Retrieve the Cosmos DB connection string from environment variables.
                string cosmosConnectionString = Environment.GetEnvironmentVariable("CosmosDBConnectionString");

                // Register CosmosClient as a singleton.
                services.AddSingleton<CosmosClient>(sp =>
                {
                    return new CosmosClient(cosmosConnectionString);
                });

                // Register services
                services.AddSingleton<IMetadataProcessor, MetadataProcessor>();
            })
            .Build();

        host.Run();
    }
}
