using Api.Interfaces;
using Api.Models;
using AzurePhotoFlow.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace unitTests;

[TestFixture]
public class GetProjectsTimestampTests
{
    [Test]
    public async Task GetProjects_ValidTimestamp_ParsesDate()
    {
        var mockService = new Mock<IImageUploadService>();
        mockService.Setup(s => s.GetProjectsAsync(null, null, It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<ProjectInfo>());

        var controller = new ImageController(new Mock<ILogger<ImageController>>().Object,
            mockService.Object, new Mock<IEmbeddingService>().Object, new Mock<IVectorStore>().Object);

        var result = await controller.GetProjects(null, null, "01/02/2025");
        Assert.IsInstanceOf<OkObjectResult>(result);
        mockService.Verify(s => s.GetProjectsAsync(null, null, It.Is<DateTime?>(d => d!.Value.Year == 2025 && d.Value.Month == 1 && d.Value.Day == 2)), Times.Once);
    }

    [Test]
    public async Task GetProjects_InvalidTimestamp_ReturnsBadRequest()
    {
        var controller = new ImageController(new Mock<ILogger<ImageController>>().Object,
            new Mock<IImageUploadService>().Object, new Mock<IEmbeddingService>().Object, new Mock<IVectorStore>().Object);

        var result = await controller.GetProjects(null, null, "notadate");
        Assert.IsInstanceOf<BadRequestObjectResult>(result);
    }
}
