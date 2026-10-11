using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Tests.Support;

namespace PlaytestTracker.Api.Tests;

// Projects and bugs belong to people who signed in: without a valid token the API must answer
// 401 and never touch the data. The ids in the paths don't need to exist: the check happens first.
public class ProtectedEndpointsTests : IDisposable
{
    private readonly ApiFactory _factory = new ApiFactory().WithEmptyDatabase();
    private readonly HttpClient _client;

    public ProtectedEndpointsTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // Every endpoint of projects and bugs: method and path
    public static TheoryData<string, string> AllDataEndpoints => new()
    {
        { "GET", "/api/projects" },
        { "GET", "/api/projects/1" },
        { "POST", "/api/projects" },
        { "PUT", "/api/projects/1" },
        { "DELETE", "/api/projects/1" },
        { "GET", "/api/bugs" },
        { "GET", "/api/bugs/1" },
        { "PUT", "/api/bugs/1" },
        { "PATCH", "/api/bugs/1" },
        { "DELETE", "/api/bugs/1" },
        { "GET", "/api/projects/1/bugs" },
        { "POST", "/api/projects/1/bugs" },
        { "DELETE", "/api/projects/1/bugs" }
    };

    private async Task<HttpResponseMessage> Send(string method, string path, string? token)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (token != null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // A body for the endpoints that read one, so a 401 can't be mistaken for a 400
        if (method is "POST" or "PUT" or "PATCH")
            request.Content = JsonContent.Create(new { });
        return await _client.SendAsync(request);
    }

    private async Task<string> SignUp()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { email = "ana@test.local", password = "Sunflower7", displayName = "Ana" });
        var body = (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        return body.Token;
    }

    [Theory]
    [MemberData(nameof(AllDataEndpoints))]
    public async Task WithoutAToken_Returns401(string method, string path)
    {
        var response = await Send(method, path, token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AllDataEndpoints))]
    public async Task WithAnInvalidToken_Returns401(string method, string path)
    {
        var response = await Send(method, path, token: "not-a-real-token");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WithAValidToken_TheRequestGoesThrough()
    {
        var token = await SignUp();

        // The database is empty, so the project doesn't exist: a 404 means the request got past
        // the token check and reached the real code (a 401 would mean it was stopped at the door)
        var response = await Send("GET", "/api/projects/1", token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // The tests above only cover the endpoints written down in AllDataEndpoints. This one looks
    // at EVERY endpoint the app has, so a controller added tomorrow can't be left open by mistake:
    // it has to be protected, or be one of the two on purpose.
    [Fact]
    public void EveryEndpointRequiresSignIn_ExceptRegisterAndLogin()
    {
        var open = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAuthorizeData>() == null)
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Order()
            .ToList();

        Assert.Equal(["api/Auth/login", "api/Auth/register"], open);
    }
}
