using Api.Interfaces;
using AzurePhotoFlow.Services;
using AzurePhotoFlow.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace unitTests;

[TestFixture]
public class EmbeddingControllerTests
{
    [Test]
    public async Task Generate_BuildsObjectKeysAndCallsService()
    {
        // Arrange
        var mockService = new Mock<IEmbeddingService>();
        var mockStore = new Mock<IVectorStore>();
        List<ImageEmbeddingInput>? received = null;
        mockService.Setup(s => s.GenerateEmbeddingsAsync(It.IsAny<IEnumerable<ImageEmbeddingInput>>()))
            .Callback<IEnumerable<ImageEmbeddingInput>>(imgs => received = imgs.ToList())
            .ReturnsAsync((IEnumerable<ImageEmbeddingInput> imgs) =>
                imgs.Select(i => new ImageEmbedding(i.ObjectKey, new float[] { 1f })).ToList());
        var controller = new EmbeddingController(mockService.Object, mockStore.Object);

        var ts = new System.DateTime(2025, 1, 1);
        var project = "Proj";
        var dirName = "CameraA";
        var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry($"{dirName}/img.jpg");
            await using var es = entry.Open();
            await es.WriteAsync(new byte[] { 1, 2, 3 });
        }
        zipStream.Position = 0;
        IFormFile file = new FormFile(zipStream, 0, zipStream.Length, "zip", "dir.zip");

        var request = new EmbeddingRequest
        {
            ProjectName = project,
            DirectoryName = dirName,
            Timestamp = ts,
            IsRawFiles = true,
            RawDirectoryName = dirName,
            ZipFile = file
        };

        // Act
        var result = await controller.Generate(request);

        mockStore.Verify(s => s.UpsertAsync(It.IsAny<IEnumerable<ImageEmbedding>>()), Times.Once);

        // Assert
        Assert.IsInstanceOf<OkResult>(result);
        Assert.NotNull(received);
        var expectedPrefix = MinIODirectoryHelper.GetDestinationPath(ts, project, dirName, true);
        Assert.AreEqual(1, received!.Count);
        Assert.AreEqual($"{expectedPrefix}/img.jpg", received[0].ObjectKey);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, received[0].ImageBytes);
    }
}

