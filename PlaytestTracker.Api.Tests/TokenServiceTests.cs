using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Options;
using PlaytestTracker.Api.Services;
using PlaytestTracker.Api.Tests.Support;

namespace PlaytestTracker.Api.Tests;

public class TokenServiceTests
{
    private static readonly ApplicationUser Ana = new()
    {
        Id = "user-id-123",
        Email = "ana@test.local",
        UserName = "ana@test.local",
        DisplayName = "Ana"
    };

    // Checks a token the way the API does on every request: with the real JwtBearer settings
    private static async Task<TokenValidationResult> Validate(TestApp app, string token)
    {
        var bearer = app.Get<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        return await new JsonWebTokenHandler().ValidateTokenAsync(token, bearer.TokenValidationParameters);
    }

    [Fact]
    public void CreateToken_StatesWhoThePersonIsAndWhenItExpires()
    {
        var now = new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
        using var app = new TestApp(clock: new FakeClock(now));

        var result = app.Get<TokenService>().CreateToken(Ana);

        var token = new JsonWebToken(result.Token);
        Assert.Equal("user-id-123", token.Subject);
        Assert.Equal("ana@test.local", token.GetPayloadValue<string>("email"));
        Assert.Equal("Ana", token.GetPayloadValue<string>("name"));
        Assert.Equal(TestApp.JwtIssuer, token.Issuer);
        Assert.Contains(TestApp.JwtAudience, token.Audiences);
        Assert.Equal(now.AddMinutes(60), result.ExpiresAt);
        Assert.Equal(now.AddMinutes(60), new DateTimeOffset(token.ValidTo));
    }

    [Fact]
    public void CreateToken_NeverPutsThePasswordHashInside()
    {
        // The payload is only encoded, not encrypted: anyone can read it
        using var app = new TestApp();
        Ana.PasswordHash = "SECRET-HASH-VALUE";

        var token = new JsonWebToken(app.Get<TokenService>().CreateToken(Ana).Token);

        Assert.DoesNotContain("SECRET-HASH-VALUE", token.EncodedPayload + token.UnsafeToString());
        Assert.DoesNotContain("SECRET-HASH-VALUE",
            System.Text.Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(token.EncodedPayload)));
    }

    [Fact]
    public void CreateToken_EachTokenHasItsOwnId()
    {
        using var app = new TestApp();
        var tokens = app.Get<TokenService>();

        var first = new JsonWebToken(tokens.CreateToken(Ana).Token);
        var second = new JsonWebToken(tokens.CreateToken(Ana).Token);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task ATokenMadeByTheApp_IsAccepted()
    {
        using var app = new TestApp();

        var result = await Validate(app, app.Get<TokenService>().CreateToken(Ana).Token);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task AnExpiredToken_IsRejected()
    {
        // Made two hours ago with a one-hour life: it expired an hour ago
        var twoHoursAgo = DateTimeOffset.UtcNow.AddHours(-2);
        using var app = new TestApp(clock: new FakeClock(twoHoursAgo));

        var result = await Validate(app, app.Get<TokenService>().CreateToken(Ana).Token);

        Assert.False(result.IsValid);
        Assert.IsType<SecurityTokenExpiredException>(result.Exception);
    }

    [Fact]
    public async Task ATokenSignedWithAnotherKey_IsRejected()
    {
        using var app = new TestApp();
        using var attacker = new TestApp(new Dictionary<string, string?>
        {
            ["Key"] = "a-different-key-a-different-key-a-different-key-999"
        });
        var forged = attacker.Get<TokenService>().CreateToken(Ana).Token;

        var result = await Validate(app, forged);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ATokenForAnotherAudience_IsRejected()
    {
        using var app = new TestApp();
        using var other = new TestApp(new Dictionary<string, string?> { ["Audience"] = "some-other-app" });
        var token = other.Get<TokenService>().CreateToken(Ana).Token;

        var result = await Validate(app, token);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ATokenFromAnotherIssuer_IsRejected()
    {
        // Same key and audience, but made by "someone else": it must not be trusted
        using var app = new TestApp();
        using var other = new TestApp(new Dictionary<string, string?> { ["Issuer"] = "some-other-api" });
        var token = other.Get<TokenService>().CreateToken(Ana).Token;

        var result = await Validate(app, token);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ATamperedToken_IsRejected()
    {
        using var app = new TestApp();
        var token = new JsonWebToken(app.Get<TokenService>().CreateToken(Ana).Token);

        // Someone edits the payload to say they are another user, and keeps the old signature
        var payload = System.Text.Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(token.EncodedPayload));
        var edited = payload.Replace("user-id-123", "user-id-999");
        var forged = $"{token.EncodedHeader}.{Base64UrlEncoder.Encode(edited)}.{token.EncodedSignature}";

        var result = await Validate(app, forged);

        Assert.False(result.IsValid);
    }

    // ---- Settings ----

    [Fact]
    public void WithoutAKey_TheSettingsAreRefusedAndTheMessageSaysHowToFixIt()
    {
        using var app = new TestApp(new Dictionary<string, string?> { ["Key"] = null });

        var error = Assert.Throws<OptionsValidationException>(() => app.Get<IOptions<JwtOptions>>().Value);

        Assert.Contains("user-secrets", error.Message);
    }

    [Fact]
    public void AKeyThatIsTooShort_IsRefused()
    {
        using var app = new TestApp(new Dictionary<string, string?> { ["Key"] = "too-short" });

        var error = Assert.Throws<OptionsValidationException>(() => app.Get<IOptions<JwtOptions>>().Value);

        Assert.Contains("at least 32", error.Message);
    }
}
