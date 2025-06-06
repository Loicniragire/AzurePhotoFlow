using Api.Interfaces;
using Api.Models;
using AzurePhotoFlow.Services;
using AzurePhotoFlow.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

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
        List<ImageEmbeddingInput>? received = null;
        mockEmbeddingService.Setup(s => s.GenerateAsync(It.IsAny<IEnumerable<ImageEmbeddingInput>>()))
            .Callback<IEnumerable<ImageEmbeddingInput>>(imgs => received = imgs.ToList())
            .Returns(Task.CompletedTask);

        var controller = new ImageController(new Mock<ILogger<ImageController>>().Object, mockUploadService.Object, mockEmbeddingService.Object);

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

        // Assert
        Assert.IsInstanceOf<OkObjectResult>(result);
        Assert.NotNull(received);
        Assert.AreEqual(1, received!.Count);
        var expectedPrefix = MinIODirectoryHelper.GetDestinationPath(ts, "proj", "directory", true);
        Assert.AreEqual($"{expectedPrefix}/dummy.txt", received[0].ObjectKey);
    }
}
