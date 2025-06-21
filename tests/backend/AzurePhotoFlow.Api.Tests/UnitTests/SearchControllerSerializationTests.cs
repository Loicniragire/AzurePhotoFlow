using System.Text.Json;
using Api.Controllers;
using Api.Interfaces;
using Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace unitTests;

[TestFixture]
public class SearchControllerSerializationTests
{
    [Test]
    public async Task SemanticSearch_Response_UsesCamelCaseProperties()
    {
        var logger = new Mock<ILogger<SearchController>>();
        var embedding = new Mock<IEmbeddingService>();
        embedding.Setup(e => e.GenerateTextEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(new float[] { 0.1f });

        var store = new Mock<IVectorStore>();
        store.Setup(s => s.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<Dictionary<string, object>>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new VectorSearchResult
                {
                    ObjectKey = "img.jpg",
                    SimilarityScore = 0.9,
                    Metadata = new Dictionary<string, object>{{"path","2024/ts/project/dir/img.jpg"}}
                }
            });
        store.Setup(s => s.GetTotalCountAsync(It.IsAny<Dictionary<string, object>>()))
            .ReturnsAsync(1);
        store.Setup(s => s.GetCollectionNameAsync()).ReturnsAsync("images");

        var controller = new SearchController(logger.Object, embedding.Object, store.Object);

        var result = await controller.SemanticSearch("test");
        var ok = result.Result as OkObjectResult;
        Assert.NotNull(ok);

        var json = JsonSerializer.Serialize(ok!.Value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("query", out _));
        Assert.True(doc.RootElement.TryGetProperty("results", out var results));
        Assert.True(results[0].TryGetProperty("objectKey", out _));
    }
}
