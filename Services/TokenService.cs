using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Options;

namespace PlaytestTracker.Api.Services
{
    public class TokenResult
    {
        public string Token { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
    }

    // Creates the JWT a person receives after logging in. A JWT is three parts joined by dots,
    // header.payload.signature: the payload says who the person is, and the signature proves
    // it was made by this API (only someone with the secret key could produce it).
    // The payload is encoded, NOT encrypted: anyone can read it, so no secrets go inside.
    public class TokenService
    {
        private readonly JwtOptions _options;
        private readonly TimeProvider _time;

        // TimeProvider is the clock. Asking for it, instead of calling DateTime.UtcNow directly,
        // lets the tests use a fake clock and check expiration without waiting an hour
        public TokenService(IOptions<JwtOptions> options, TimeProvider time)
        {
            _options = options.Value;
            _time = time;
        }

        public TokenResult CreateToken(ApplicationUser user)
        {
            var now = _time.GetUtcNow();
            var expiresAt = now.AddMinutes(_options.ExpiresMinutes);

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime,
                Expires = expiresAt.UtcDateTime,
                // "Claims" are the facts the token states about the person
                Claims = new Dictionary<string, object>
                {
                    [JwtRegisteredClaimNames.Sub] = user.Id, // the stable id: the email could change
                    [JwtRegisteredClaimNames.Email] = user.Email!,
                    ["name"] = user.DisplayName,
                    [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString() // a unique id for this token
                },
                SigningCredentials = new SigningCredentials(
                    CreateKey(_options), SecurityAlgorithms.HmacSha256)
            };

            return new TokenResult
            {
                Token = new JsonWebTokenHandler().CreateToken(descriptor),
                ExpiresAt = expiresAt
            };
        }

        public static SymmetricSecurityKey CreateKey(JwtOptions options)
            => new(Encoding.UTF8.GetBytes(options.Key));
    }
}
