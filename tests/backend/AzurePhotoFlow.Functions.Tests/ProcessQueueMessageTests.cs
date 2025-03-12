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
        var loggerFactory = new LoggerFactory();
        var metadataProcessorMock = new Mock<IMetadataProcessor>();
		metadataProcessorMock.Setup(x => x.ProcessAsync(It.IsAny<ImageMetadata>())).Returns(Task.CompletedTask);

        var functionApp = new ProcessQueueMessage(metadataProcessorMock.Object, loggerFactory);

        var request = new ImageMetadata[] { new ImageMetadata() {
                   Id = "1",
				   BlobUri = "https://test.blob.core.windows.net/test/test.jpg"
            }
           };

        await functionApp.MetadataBatchProcessor(request);

    }
}
