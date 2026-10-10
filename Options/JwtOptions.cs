using System.ComponentModel.DataAnnotations;

namespace PlaytestTracker.Api.Options
{
    // The "Jwt" section of the configuration. Issuer, Audience and ExpiresMinutes are not secret
    // and live in appsettings.json. The Key IS secret and never goes in the repo:
    // in development it comes from user-secrets, in production from an environment variable.
    public class JwtOptions
    {
        public const string SectionName = "Jwt";

        // Who creates the tokens (this API) and who they are meant for (the web app).
        // A token for another audience is refused, even if the signature is right.
        [Required]
        public string Issuer { get; set; } = string.Empty;

        [Required]
        public string Audience { get; set; } = string.Empty;

        [Range(1, 24 * 60)]
        public int ExpiresMinutes { get; set; } = 60;

        // The secret that signs every token. Whoever has it can make a token for anybody,
        // so it must be long enough to be impossible to guess.
        [Required(ErrorMessage =
            "Jwt:Key is missing. In development run: dotnet user-secrets set \"Jwt:Key\" \"<32 or more random characters>\"")]
        [MinLength(32, ErrorMessage = "Jwt:Key must be at least 32 characters long.")]
        public string Key { get; set; } = string.Empty;
    }
}
