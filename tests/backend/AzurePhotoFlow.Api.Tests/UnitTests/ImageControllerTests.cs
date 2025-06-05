using Api.Interfaces;
using AzurePhotoFlow.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace unitTests;

[TestFixture]
public class ImageControllerTests
{
    [Test]
    public async Task UploadDirectory_SendsArchiveToEmbeddingService()
    {
        // Arrange
        var mockUploadService = new Mock<IImageUploadService>();
        mockUploadService
            .Setup(s => s.ExtractAndUploadImagesAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), true, ""))
            .ReturnsAsync(new UploadResponse());

        HttpRequestMessage? capturedRequest = null;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
        var client = new HttpClient(handler.Object) { BaseAddress = new Uri("http://embedding") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("EmbeddingService")).Returns(client);

        var controller = new ImageController(new Mock<ILogger<ImageController>>().Object, mockUploadService.Object, factory.Object);

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
        var result = await controller.UploadDirectory(DateTime.UtcNow, "proj", file);

        // Assert
        Assert.IsInstanceOf<OkObjectResult>(result);
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(HttpMethod.Post, capturedRequest!.Method);
        Assert.IsTrue(capturedRequest.RequestUri!.AbsoluteUri.EndsWith("generate"));
        Assert.IsInstanceOf<MultipartFormDataContent>(capturedRequest.Content);
        var multipart = (MultipartFormDataContent)capturedRequest.Content!;
        string? isRaw = null;
        string? rawDir = null;
        foreach (var part in multipart)
        {
            var name = part.Headers.ContentDisposition?.Name?.Trim('"');
            if (name == "IsRawFiles")
                isRaw = await part.ReadAsStringAsync();
            if (name == "RawDirectoryName")
                rawDir = await part.ReadAsStringAsync();
        }
        Assert.AreEqual("true", isRaw);
        Assert.AreEqual("directory", rawDir);
    }
}
