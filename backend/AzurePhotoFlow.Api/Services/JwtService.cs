using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

public class JwtService
{
    private readonly SymmetricSecurityKey _jwtKey;
    private const string Issuer = "loicportraits.azurewebsites.net";
    private const string Audience = "loicportraits.azurewebsites.net";

    public JwtService(SymmetricSecurityKey jwtKey)
    {
        _jwtKey = jwtKey;
    }

    public string GenerateJwtToken(string userId, string email)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("role", "FullAccess") // Adjust for RBAC if needed
        };

        var creds = new SigningCredentials(_jwtKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7), // Token valid for 7 days
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

