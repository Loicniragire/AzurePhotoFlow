using Api.Models;
using AzurePhotoFlow.Services;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;

namespace unitTests;

[TestFixture]
public class JwtServiceTests
{
    [Test]
    public void GenerateJwtToken_UsesConfiguredIssuerAndAudience()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"));
        var config = new JwtConfig { Issuer = "test-issuer", Audience = "test-audience" };
        var service = new JwtService(key, config);

        var tokenString = service.GenerateJwtToken("user1", "user@test.com");
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        Assert.AreEqual("test-issuer", token.Issuer);
        Assert.AreEqual("test-audience", token.Audiences.Single());
    }

    [Test]
    public void ValidateToken_WithConfiguredParameters_Succeeds()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"));
        var config = new JwtConfig { Issuer = "test-issuer", Audience = "test-audience" };
        var service = new JwtService(key, config);

        var tokenString = service.GenerateJwtToken("user1", "user@test.com");

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config.Issuer,
            ValidateAudience = true,
            ValidAudience = config.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = false
        };

        var handler = new JwtSecurityTokenHandler();
        handler.ValidateToken(tokenString, parameters, out var validatedToken);
        Assert.NotNull(validatedToken);
    }
}
