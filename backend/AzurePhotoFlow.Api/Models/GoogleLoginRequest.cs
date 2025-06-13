using System.ComponentModel.DataAnnotations;

namespace Api.Models;

public class GoogleLoginRequest
{
    [Required]
    public string Token { get; set; } = string.Empty; // Google ID Token
}

