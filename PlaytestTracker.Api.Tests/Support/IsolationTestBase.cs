using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.Tests.Support;

// What the tests about "who can see what" have in common: a running API, people who sign up,
// and helpers to act as one of them. Ana and Ben each get a personal organization (Owner of it).
public abstract class IsolationTestBase : IDisposable
{
    protected readonly ApiFactory Factory;
    protected readonly HttpClient Client;

    // The API sends enums as text ("High"), so reading its answers needs to know that too
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected Person Ana = null!;
    protected Person Ben = null!;

    // demo: true turns the demo organization on (it is created by the test that needs it)
    protected IsolationTestBase(bool demo = false)
    {
        Factory = new ApiFactory(demo: demo).WithEmptyDatabase();
        Client = Factory.CreateClient();
    }

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
    }

    // A person who signed up: their token, their id, and the personal organization they got
    protected sealed record Person(string Token, string Id, int OrganizationId);

    protected async Task SignUpAnaAndBen()
    {
        Ana = await SignUp("ana");
        Ben = await SignUp("ben");
    }

    protected async Task<Person> SignUp(string name)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register",
            new { email = $"{name}@test.local", password = "Sunflower7", displayName = name });
        var body = (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var organizationId = await db.Memberships
            .Where(m => m.UserId == body.User.Id)
            .Select(m => m.OrganizationId)
            .SingleAsync();

        return new Person(body.Token, body.User.Id, organizationId);
    }

    // Makes a person a Member of someone else's organization (there is no endpoint for this yet)
    protected async Task AddAsMember(Person person, int organizationId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Memberships.Add(new Membership
        {
            OrganizationId = organizationId,
            UserId = person.Id,
            Role = OrganizationRole.Member,
            JoinedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    protected Task<HttpResponseMessage> Send(Person person, HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", person.Token);
        if (body != null)
            request.Content = JsonContent.Create(body);
        return Client.SendAsync(request);
    }

    protected async Task<ProjectDto> CreateProject(
        Person person, int organizationId, string name = "Pink in the Night", string code = "PIN")
    {
        var response = await Send(person, HttpMethod.Post, $"/api/organizations/{organizationId}/projects",
            new { name, code });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
    }

    protected async Task<BugDto> CreateBug(Person person, int projectId, string title = "Neon sign flickers")
    {
        var response = await Send(person, HttpMethod.Post, $"/api/projects/{projectId}/bugs",
            new { title, description = "It flickers much too fast.", severity = "High" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BugDto>(Json))!;
    }

    protected async Task<BugDto> GetBug(Person person, int bugId)
    {
        var response = await Send(person, HttpMethod.Get, $"/api/bugs/{bugId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BugDto>(Json))!;
    }
}
