using Microsoft.Extensions.Logging;
using AzurePhotoFlow.Services;
using Moq;
namespace UnitTests;

[TestFixture]
public class MetadataExtractorServiceTests
{
    private ILogger <MetadataExtractorService> _logger;
    private MetadataExtractorService _extractorService;

    [SetUp]
    public void Setup()
    {
        // Create a logger factory that logs to the console.
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        _logger = loggerFactory.CreateLogger<MetadataExtractorService>();

        // Initialize the service with the console logger.
        _extractorService = new MetadataExtractorService(_logger);
    }

    [Test]
    public void GetCameraGeneratedMetadata_ShouldReturnCameraGeneratedMetadata()
    {
        // Arrange:
        // get the image
        var imagePath = "Images/digital/1A8A9011.jpg";
        var image = TestHelper.GetEmbeddedResource(imagePath);

        // Act
        // get the camera generated metadata
        var cameraGeneratedMetadata = _extractorService.GetCameraGeneratedMetadata(image);

        // Assert
        // check if the camera generated metadata is not null
        // check if the camera generated metadata has the expected values
        Assert.IsNotNull(cameraGeneratedMetadata);

        // print the camera generated metadata to the console
        Console.WriteLine(cameraGeneratedMetadata);




    }

}

