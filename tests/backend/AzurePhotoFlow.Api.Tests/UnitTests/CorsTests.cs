using System.Net.Http;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using AzurePhotoFlow.Services;

namespace unitTests;

[TestFixture]
public class CorsTests
{
    [TearDown]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable("ALLOWED_ORIGINS", null);
    }

    [Test]
    public async Task AllowedOrigin_ReturnsCorsHeader()
    {
        Environment.SetEnvironmentVariable("ALLOWED_ORIGINS", "http://allowed.com");
        using var server = CreateServer();
        using var client = server.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/");
        request.Headers.Add("Origin", "http://allowed.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        Assert.IsTrue(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        Assert.AreEqual("http://allowed.com", values.First());
    }

    [Test]
    public async Task DisallowedOrigin_DoesNotReturnCorsHeader()
    {
        Environment.SetEnvironmentVariable("ALLOWED_ORIGINS", "http://allowed.com");
        using var server = CreateServer();
        using var client = server.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/");
        request.Headers.Add("Origin", "http://other.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        Assert.IsFalse(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static TestServer CreateServer()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                var origins = CorsConfigHelper.GetAllowedOrigins();
                services.AddCors(options =>
                {
                    options.AddPolicy("AllowSpecificOrigin", policy =>
                    {
                        policy.WithOrigins(origins)
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                            .AllowCredentials();
                    });
                });
            })
            .Configure(app =>
            {
                app.UseCors("AllowSpecificOrigin");
                app.Run(async ctx => await ctx.Response.WriteAsync("ok"));
            });

        return new TestServer(builder);
    }
}
