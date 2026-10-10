using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Tests.Support;

namespace PlaytestTracker.Api.Tests;

// These tests call the API the way the frontend does: HTTP requests with JSON bodies, and
// they check status codes and response bodies. A new API (and database) for every test.
public class AuthEndpointsTests : IDisposable
{
    // Test values only
    private const string Password = "Sunflower7";
    private const string WrongPassword = "Wrongpass1";

    private readonly ApiFactory _factory = new ApiFactory().WithEmptyDatabase();
    private readonly HttpClient _client;

    public AuthEndpointsTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private Task<HttpResponseMessage> Register(
        string email = "ana@test.local", string password = Password, string displayName = "Ana")
        => _client.PostAsJsonAsync("/api/auth/register", new { email, password, displayName });

    private Task<HttpResponseMessage> Login(string email = "ana@test.local", string password = Password)
        => _client.PostAsJsonAsync("/api/auth/login", new { email, password });

    private async Task<HttpResponseMessage> Me(string? token, HttpClient? client = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        if (token != null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await (client ?? _client).SendAsync(request);
    }

    private static async Task<AuthResponseDto> Body(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;

    private static async Task<ValidationProblemDetails> Problem(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!;

    // ---- POST /api/auth/register ----

    [Fact]
    public async Task Register_WithValidData_Returns201WithATokenAndTheUser()
    {
        var response = await Register();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await Body(response);
        Assert.False(string.IsNullOrEmpty(body.Token));
        Assert.True(body.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal("ana@test.local", body.User.Email);
        Assert.Equal("Ana", body.User.DisplayName);
        Assert.False(string.IsNullOrEmpty(body.User.Id));
    }

    [Fact]
    public async Task Register_NeverReturnsThePasswordOrItsHash()
    {
        var response = await Register();

        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Password, text);
        Assert.DoesNotContain("passwordHash", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_TheTokenItReturnsWorksRightAway()
    {
        var registered = await Body(await Register());

        var response = await Me(registered.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_EmailAlreadyUsed_Returns409WithTheMessageOnTheEmailField()
    {
        await Register("ana@test.local");

        var response = await Register("ANA@test.local");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Email", (await Problem(response)).Errors.Keys);
    }

    [Fact]
    public async Task Register_WeakPassword_Returns400WithTheReasonOnThePasswordField()
    {
        var response = await Register(password: "abcdef1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await Problem(response)).Errors;
        Assert.Contains(errors["Password"], message => message.Contains("at least 8"));
    }

    [Fact]
    public async Task Register_EmptyBody_Returns400WithAnErrorForEachField()
    {
        var response = await Register(email: "", password: "", displayName: "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await Problem(response)).Errors.Keys;
        Assert.Contains("Email", errors);
        Assert.Contains("Password", errors);
        Assert.Contains("DisplayName", errors);
    }

    [Fact]
    public async Task Register_NotAnEmail_Returns400()
    {
        var response = await Register(email: "not-an-email");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Email", (await Problem(response)).Errors.Keys);
    }

    // ---- POST /api/auth/login ----

    [Fact]
    public async Task Login_WithTheCorrectPassword_Returns200WithATokenThatWorks()
    {
        await Register();

        var response = await Login();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Body(response);
        Assert.Equal("Ana", body.User.DisplayName);
        Assert.Equal(HttpStatusCode.OK, (await Me(body.Token)).StatusCode);
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_Return401WithTheSameMessage()
    {
        await Register();

        var wrongPassword = await Login(password: WrongPassword);
        var unknownEmail = await Login(email: "ghost@test.local", password: WrongPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(
            (await Problem(wrongPassword)).Title,
            (await Problem(unknownEmail)).Title);
    }

    [Fact]
    public async Task Login_AfterFiveWrongPasswords_Returns423EvenWithTheCorrectPassword()
    {
        await Register();
        for (var attempt = 1; attempt <= 5; attempt++)
            await Login(password: WrongPassword);

        var response = await Login(password: Password);

        Assert.Equal(HttpStatusCode.Locked, response.StatusCode);
        Assert.Contains("minute", (await Problem(response)).Detail);
    }

    // ---- GET /api/auth/me ----

    [Fact]
    public async Task Me_WithAValidToken_ReturnsWhoThePersonIs()
    {
        var token = (await Body(await Register(displayName: "Ana"))).Token;

        var response = await Me(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = (await response.Content.ReadFromJsonAsync<UserDto>())!;
        Assert.Equal("ana@test.local", user.Email);
        Assert.Equal("Ana", user.DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not.a.token")]
    [InlineData("garbage")]
    public async Task Me_WithoutAValidToken_Returns401(string? token)
    {
        var response = await Me(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithAnExpiredToken_Returns401()
    {
        // This API's clock says it is two hours ago, so the token it hands out is already expired
        using var oldClockFactory = new ApiFactory(clock: new FakeClock(DateTimeOffset.UtcNow.AddHours(-2)))
            .WithEmptyDatabase();
        using var oldClockClient = oldClockFactory.CreateClient();
        var registered = await Body(await oldClockClient.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "ana@test.local", password = Password, displayName = "Ana" }));

        var response = await Me(registered.Token, oldClockClient);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithATokenSignedByAnotherApi_Returns401()
    {
        // Another API, with another secret key, makes a token for the same email
        using var otherFactory = new ApiFactory(jwtKey: "another-secret-another-secret-another-secret-1").WithEmptyDatabase();
        using var otherClient = otherFactory.CreateClient();
        var forged = await Body(await otherClient.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "ana@test.local", password = Password, displayName = "Ana" }));
        await Register();

        var response = await Me(forged.Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_AfterTheAccountIsDeleted_Returns401()
    {
        var token = (await Body(await Register())).Token;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users.ExecuteDeleteAsync();
        }

        // The token is still correctly signed and not expired, but the person no longer exists
        var response = await Me(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
