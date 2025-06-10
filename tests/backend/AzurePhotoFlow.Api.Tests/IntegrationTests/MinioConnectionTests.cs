using Minio;
using NUnit.Framework;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace integrationTests;

[TestFixture]
public class MinioConnectionTests
{
    private static string Endpoint => Environment.GetEnvironmentVariable("MINIO_ENDPOINT") ?? "http://localhost:9000";
    private static string AccessKey => Environment.GetEnvironmentVariable("MINIO_ACCESS_KEY") ?? "minioadmin";
    private static string SecretKey => Environment.GetEnvironmentVariable("MINIO_SECRET_KEY") ?? "minioadmin";

    private static async Task<bool> IsMinioRunning()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
            var resp = await http.GetAsync($"{Endpoint.TrimEnd('/')}/minio/health/live");
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    [Test]
    public async Task ListBuckets_WhenMinioRunning_ReturnsResult()
    {
        if (!await IsMinioRunning())
        {
            Assert.Ignore("MinIO service not running");
        }

        var client = new MinioClient()
            .WithEndpoint(Endpoint)
            .WithCredentials(AccessKey, SecretKey)
            .Build();

        var buckets = await client.ListBucketsAsync();
        Assert.IsNotNull(buckets);
    }
}

