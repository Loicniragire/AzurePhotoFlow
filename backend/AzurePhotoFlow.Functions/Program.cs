using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Extensions.DependencyInjection;
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

// Create the builder using the ASP.NET Core integration model (preview)
var builder = FunctionsWebApplication.CreateBuilder(args);

// Configure Application Insights and logging
builder.Services.AddApplicationInsightsTelemetryWorkerService();
builder.Services.ConfigureFunctionsApplicationInsights();
builder.Services.Configure<LoggerFilterOptions>(options =>
{
    var ruleToRemove = options.Rules.FirstOrDefault(rule => 
        rule.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
    if (ruleToRemove is not null)
    {
        options.Rules.Remove(ruleToRemove);
    }
});

// Cosmos DB configuration: Retrieve the connection string from configuration
var cosmosConnectionString = builder.Configuration["CosmosDBConnectionString"] 
    ?? throw new InvalidOperationException("Missing CosmosDB connection string");

// Register the CosmosClient as a singleton
builder.Services.AddSingleton(new CosmosClient(
    cosmosConnectionString,
    new CosmosClientOptions
    {
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        }
    }));

// Register custom application services
builder.Services.AddSingleton<IMetadataProcessor, ImageMetadataProcessor>();

// Build the application with ASP.NET Core integration enabled
var app = builder.Build();

// Map all function endpoints into the ASP.NET Core middleware pipeline
app.MapFunctions();

// Run the application
app.Run();

