using Api.Interfaces;
using Api.Models;
using AzurePhotoFlow.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO.Compression;

namespace unitTests;

[TestFixture]
public class ImageControllerTests
{
    [Test]
    public async Task UploadDirectory_CallsEmbeddingServiceWithImages()
    {
        // Arrange
        var mockUploadService = new Mock<IImageUploadService>();
        mockUploadService
            .Setup(s => s.ExtractAndUploadImagesAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), true, ""))
            .ReturnsAsync(new UploadResponse());
        var mockEmbeddingService = new Mock<IEmbeddingService>();
        var mockStore = new Mock<IVectorStore>();
        List<ImageEmbeddingInput>? received = null;
        mockEmbeddingService.Setup(s => s.GenerateEmbeddingsAsync(It.IsAny<IAsyncEnumerable<ImageEmbeddingInput>>()))
            .Returns((IAsyncEnumerable<ImageEmbeddingInput> imgs) => Record(imgs));

        async IAsyncEnumerable<ImageEmbedding> Record(IAsyncEnumerable<ImageEmbeddingInput> imgs)
        {
            received = new List<ImageEmbeddingInput>();
            await foreach (var i in imgs)
            {
                received.Add(i);
                yield return new ImageEmbedding(i.ObjectKey, new float[] { 1f });
            }
        }

        var controller = new ImageController(new Mock<ILogger<ImageController>>().Object, mockUploadService.Object, mockEmbeddingService.Object, mockStore.Object);

        var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("dummy.txt");
            await using var entryStream = entry.Open();
            await entryStream.WriteAsync(new byte[] { 1, 2, 3 });
        }
        zipStream.Position = 0;
        IFormFile file = new FormFile(zipStream, 0, zipStream.Length, "directory", "dummy.zip");

        // Act
        var ts = new System.DateTime(2025,1,1);
        var result = await controller.UploadDirectory(ts, "proj", file);

        mockStore.Verify(s => s.UpsertAsync(It.IsAny<IEnumerable<ImageEmbedding>>()), Times.Once);

        // Assert
        Assert.IsInstanceOf<OkObjectResult>(result);
        Assert.NotNull(received);
        Assert.AreEqual(1, received!.Count);
        var expectedPrefix = MinIODirectoryHelper.GetDestinationPath(ts, "proj", "directory", true);
        Assert.AreEqual($"{expectedPrefix}/dummy.txt", received[0].ObjectKey);
    }
}
