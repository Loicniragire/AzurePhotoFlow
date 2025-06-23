using Api.Interfaces;
using Api.Models;
using AzurePhotoFlow.Api.Interfaces;
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
        
        // Create mock embeddings input to simulate what the service would return
        var mockEmbeddingInputs = new List<ImageEmbeddingInput>
        {
            new ImageEmbeddingInput($"{MinIODirectoryHelper.GetDestinationPath(new DateTime(2025,1,1), "proj", "directory", true)}/dummy.txt", new byte[] { 1, 2, 3 })
        };
        
        mockUploadService
            .Setup(s => s.ProcessZipOptimizedAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), true, It.IsAny<string>()))
            .ReturnsAsync((new UploadResponse(), mockEmbeddingInputs));
            
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

        var mockImageMappingRepo = new Mock<IImageMappingRepository>();
        var controller = new ImageController(new Mock<ILogger<ImageController>>().Object, mockUploadService.Object, mockEmbeddingService.Object, mockStore.Object, mockImageMappingRepo.Object);

        Environment.SetEnvironmentVariable("ENABLE_EMBEDDINGS", "true");

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

    [Test]
    public async Task UploadDirectory_MissingProjectName_ReturnsBadRequest()
    {
        var mockUploadService = new Mock<IImageUploadService>();
        var mockImageMappingRepo = new Mock<IImageMappingRepository>();
        var controller = new ImageController(new Mock<ILogger<ImageController>>().Object,
                                            mockUploadService.Object,
                                            new Mock<IEmbeddingService>().Object,
                                            new Mock<IVectorStore>().Object,
                                            mockImageMappingRepo.Object);

        var stream = new MemoryStream(new byte[] {1});
        IFormFile file = new FormFile(stream, 0, stream.Length, "file", "f.zip");

        var result = await controller.UploadDirectory(DateTime.UtcNow, "", file);

        Assert.IsInstanceOf<BadRequestObjectResult>(result);
    }

    [Test]
    public async Task UploadDirectory_MissingFile_ReturnsBadRequest()
    {
        var mockImageMappingRepo = new Mock<IImageMappingRepository>();
        var controller = new ImageController(new Mock<ILogger<ImageController>>().Object,
                                            new Mock<IImageUploadService>().Object,
                                            new Mock<IEmbeddingService>().Object,
                                            new Mock<IVectorStore>().Object,
                                            mockImageMappingRepo.Object);

        var result = await controller.UploadDirectory(DateTime.UtcNow, "proj", null!);

        Assert.IsInstanceOf<BadRequestObjectResult>(result);
    }
}
