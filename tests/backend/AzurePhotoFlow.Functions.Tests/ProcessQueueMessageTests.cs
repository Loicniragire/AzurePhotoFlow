using Moq;
using Functions.Interfaces;
using Functions.Triggers;
using Microsoft.Extensions.Logging;
using AzurePhotoFlow.POCO.QueueModels;

namespace UnitTests;

[TestFixture]
public class ProcessQueueMessageTests
{
    [Test]
    public async Task ProcessQueueMessage_Should_use_runtime_to_deserialize_message_string()
    {
        // Arrange
        var loggerMock = new Mock<ILogger>();
        var loggerFactory = new Mock<ILoggerFactory>();
        var metadataProcessorMock = new Mock<IMetadataProcessor>();

        loggerFactory
            .Setup(x => x.CreateLogger(It.Is<string>(s => s == typeof(ProcessQueueMessage).FullName)))
            .Returns(loggerMock.Object);

        metadataProcessorMock
               .Setup(x => x.ProcessAsync(It.IsAny<ImageMetadata>()))
               .Returns(Task.CompletedTask);

        var functionApp = new ProcessQueueMessage(metadataProcessorMock.Object, loggerFactory.Object);
        var request = new ImageMetadata[] {
               new ImageMetadata() {
                   Id = "1",
                   BlobUri = "https://test.blob.core.windows.net/test/test.jpg"
            }
        };

        // Act
        await functionApp.MetadataBatchProcessor(request);

        // Assert
        metadataProcessorMock.Verify(x => x.ProcessAsync(It.IsAny<ImageMetadata>()), Times.Once);

        // assert that logger was called
        loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString().Contains("MetadataBatchProcessor Processing 1 queue items.")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
    }
}
