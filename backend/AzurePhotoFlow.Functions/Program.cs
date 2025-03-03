using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Cosmos;
using Functions.Interfaces;
using Functions.Services;
using Microsoft.Extensions.Logging;

namespace AzurePhotoFlow.Functions;

class Program
{
    static async Task Main(string[] args)
    {
        DotNetEnv.Env.Load();

        // Print all environment variables for debugging
        foreach (System.Collections.DictionaryEntry envVar in Environment.GetEnvironmentVariables())
        {
            Console.WriteLine($"{envVar.Key} = {envVar.Value}");
        }

        var host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureServices(ConfigureServices)
            .Build();
        try
        {

            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
        }
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.Configure<LoggerFilterOptions>(options =>
        {
            LoggerFilterRule? toRemove = options.Rules.FirstOrDefault(rule => rule.ProviderName
                == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");

            if (toRemove is not null)
            {
                options.Rules.Remove(toRemove);
            }
        });

        // Cosmos DB configuration
        var cosmosConnectionString = context.Configuration.GetValue<string>("CosmosDBConnectionString")
            ?? throw new InvalidOperationException("Missing CosmosDB connection string");

        services.AddSingleton<CosmosClient>(_ => new CosmosClient(
            cosmosConnectionString,
            new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            }));

        // Application services
        services.AddSingleton<IMetadataProcessor, ImageMetadataProcessor>();

    }
}
