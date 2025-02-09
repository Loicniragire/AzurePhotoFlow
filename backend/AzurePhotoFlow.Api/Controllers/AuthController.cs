using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Api.Models;
using Microsoft.AspNetCore.Authentication;

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
    /// Login with Google
    /// </summary>
    /// <param name="request">GoogleLoginRequest</param>
    /// <returns>ActionResult</returns>
    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(request.Token, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _googleConfig.ClientId }
            });

            // Generate JWT Token using JwtService
            var jwtToken = _jwtService.GenerateJwtToken(payload.Subject, payload.Email);

            // Set HTTP-Only Cookie
            Response.Cookies.Append("jwt", jwtToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7)
            });

            return Ok(new
            {
                message = "Login successful",
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
    public async Task<IActionResult> Logout()
    {
        // Replace "Cookies" with your specific authentication scheme if different.
        try
        {
            await HttpContext.SignOutAsync("Cookies");
            return Ok(new { message = "Successfully logged out" });
        }
		catch (Exception ex)
		{
			return BadRequest(new { message = "Error logging out", error = ex.Message });
		}
    }

    [HttpGet("check")]
    public IActionResult CheckAuthStatus()
    {
        var jwt = Request.Cookies["jwt"];

        if (string.IsNullOrEmpty(jwt))
        {
            return Ok(new { isAuthenticated = false });
        }

        return Ok(new { isAuthenticated = true });
    }
}

