using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Api.Models;
using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly JwtService _jwtService;
    private readonly GoogleConfig _googleConfig;

    public AuthController(JwtService jwtService, GoogleConfig googleConfig)
    {
        _jwtService = jwtService;
        _googleConfig = googleConfig;
    }

    /// <summary>
    /// Login with Google and obtain a JWT token.
    /// </summary>
    /// <param name="request">GoogleLoginRequest containing the Google token</param>
    /// <returns>ActionResult with the JWT token and user information</returns>
	[AllowAnonymous]
    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        try
        {
            // Validate the token using Google's libraries.
            var payload = await GoogleJsonWebSignature.ValidateAsync(request.Token, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _googleConfig.ClientId }
            });

            // Generate a JWT token using your JwtService.
            var jwtToken = _jwtService.GenerateJwtToken(payload.Subject, payload.Email);

            // Return the token and user details in the response.
            return Ok(new
            {
                message = "Login successful",
                token = jwtToken,
                googleId = payload.Subject,
                email = payload.Email,
                name = payload.Name,
                picture = payload.Picture
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "Invalid Google token", error = ex.Message });
        }
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return Ok(new { message = "Logout successful. Please discard the token on the client." });
    }

	[Authorize]
    [HttpGet("check")]
    [Authorize]
    public IActionResult CheckAuthStatus()
    {
        return Ok(new { isAuthenticated = true, user = User.Identity.Name });
    }
}

