using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Api.Models;
using System.Threading.Tasks;

/// <summary>
/// Authentication and authorization endpoints for Google OAuth and JWT token management.
/// </summary>
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
    /// Authenticate with Google OAuth and obtain a JWT token for API access.
    /// </summary>
    /// <param name="request">Google login request containing the Google ID token from OAuth flow</param>
    /// <returns>JWT token and user information for authenticated API access</returns>
    /// <response code="200">Successfully authenticated and JWT token generated</response>
    /// <response code="400">Invalid Google token or authentication failed</response>
    /// <remarks>
    /// This endpoint validates the Google ID token received from the OAuth flow and generates
    /// a JWT token that can be used to authenticate subsequent API requests.
    /// 
    /// The Google ID token should be obtained from the Google OAuth 2.0 flow on the client side.
    /// The response includes the JWT token that should be used in the Authorization header
    /// as "Bearer {token}" for authenticated API calls.
    /// 
    /// Example request:
    /// 
    ///     POST /api/auth/google-login
    ///     {
    ///         "token": "google_id_token_from_oauth_flow"
    ///     }
    /// 
    /// Example response:
    /// 
    ///     {
    ///         "message": "Login successful",
    ///         "token": "jwt_token_for_api_access",
    ///         "googleId": "user_google_id",
    ///         "email": "user@example.com",
    ///         "name": "User Name",
    ///         "picture": "https://profile-picture-url"
    ///     }
    /// 
    /// </remarks>
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

    /// <summary>
    /// Logout from the current session.
    /// </summary>
    /// <returns>Confirmation message for successful logout</returns>
    /// <response code="200">Successfully logged out</response>
    /// <remarks>
    /// Since JWT tokens are stateless, this endpoint primarily serves as a confirmation
    /// that the client should discard the token. The actual token invalidation should
    /// be handled on the client side by removing the token from storage.
    /// 
    /// Example response:
    /// 
    ///     {
    ///         "message": "Logout successful. Please discard the token on the client."
    ///     }
    /// 
    /// </remarks>
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return Ok(new { message = "Logout successful. Please discard the token on the client." });
    }

    /// <summary>
    /// Check the current authentication status and validate the JWT token.
    /// </summary>
    /// <returns>Authentication status and user information</returns>
    /// <response code="200">Token is valid and user is authenticated</response>
    /// <response code="401">Token is invalid or expired</response>
    /// <remarks>
    /// This endpoint validates the JWT token provided in the Authorization header
    /// and returns the current authentication status along with user information.
    /// 
    /// Requires Authorization header: Bearer {jwt_token}
    /// 
    /// Example response:
    /// 
    ///     {
    ///         "isAuthenticated": true,
    ///         "user": "user_identifier"
    ///     }
    /// 
    /// </remarks>
	[Authorize]
    [HttpGet("check")]
    public IActionResult CheckAuthStatus()
    {
        return Ok(new { isAuthenticated = true, user = User.Identity.Name });
    }
}

