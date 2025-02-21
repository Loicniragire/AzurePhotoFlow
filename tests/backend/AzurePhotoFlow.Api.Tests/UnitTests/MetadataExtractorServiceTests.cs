using Microsoft.Extensions.Logging;
using AzurePhotoFlow.Services;
using Api.Models;
namespace UnitTests;

[TestFixture]
public class MetadataExtractorServiceTests
{
    private ILogger<MetadataExtractorService> _logger;
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

    [TestCase("digital/1A8A9011.jpg")]
    [TestCase("film/img20220920_0124.jpg")]
    [TestCase("raw/1A8A9109.CR3")]
    public void Should_retrieve_camera_generated_metadata_from_digitally_processed_image(string path)
    {
        // Arrange:
        // get the image
        var imagePath = $"Images/{path}";
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

    [TestCase("digital/1A8A9011.jpg")]
    [TestCase("film/img20220920_0124.jpg")]
    [TestCase("raw/1A8A9109.CR3")]
    public void Metadata_size_should_fit_64KB(string path)
    {
        // Arrange:
        // get the image
        var imagePath = $"Images/{path}";
        var image = TestHelper.GetEmbeddedResource(imagePath);

        // Act
        // get the camera generated metadata
        var metadata = new ImageMetadata()
        {
            Id = Guid.NewGuid().ToString(),
            BlobUri = "https://photoflow.blob.core.windows.net/images/1A8A9011.jpg",
            UploadedBy = "testuser",
            UploadDate = DateTime.Now,
            CameraGeneratedMetadata = _extractorService.GetCameraGeneratedMetadata(image)
        };

        // Assert
        // check if the camera generated metadata size is less than or equal to 64KB
        var size = ObjectSizeCalculator.GetSerializedSizeInKB(metadata);
        Console.WriteLine($"Metadata size: {size} KB");
        Assert.LessOrEqual(size, 64);
    }

}

