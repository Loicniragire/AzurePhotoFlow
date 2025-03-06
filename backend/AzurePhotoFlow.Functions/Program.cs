using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using Functions.Interfaces;
using Functions.Services;

// Load environment variables from a .env file (for local development)
DotNetEnv.Env.Load();

// Print all environment variables for debugging
foreach (System.Collections.DictionaryEntry envVar in Environment.GetEnvironmentVariables())
{
    Console.WriteLine($"{envVar.Key} = {envVar.Value}");
}

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults() // Use the standard isolated worker defaults.
    .ConfigureServices((context, services) =>
    {
        // Configure Application Insights and logging
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.Configure<LoggerFilterOptions>(options =>
        {
            var ruleToRemove = options.Rules.FirstOrDefault(rule =>
                rule.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
            if (ruleToRemove is not null)
            {
                options.Rules.Remove(ruleToRemove);
            }
        });

        // Cosmos DB configuration: Retrieve the connection string from configuration
        var cosmosConnectionString = context.Configuration["CosmosDBConnectionString"]
            ?? throw new InvalidOperationException("Missing CosmosDB connection string");

        services.AddSingleton(new CosmosClient(
            cosmosConnectionString,
            new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            }));

        // Register custom application services
        services.AddSingleton<IMetadataProcessor, ImageMetadataProcessor>();
    })
    .Build();

host.Run();

