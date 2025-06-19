using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

public class JwtService
{
    private readonly SymmetricSecurityKey _jwtKey;
    private const string Issuer = "photoflow.app";
    private const string Audience = "photoflow.app";

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

			// Adjust for RBAC if needed. Consider integrating ASP.NET Identity for better user management 
            new Claim("role", "FullAccess")
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

