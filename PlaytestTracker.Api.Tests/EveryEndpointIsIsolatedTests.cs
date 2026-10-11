using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Tests.Support;

namespace PlaytestTracker.Api.Tests;

// The safety net for "nobody sees what belongs to another organization".
// Two tests work together:
//  1. An outsider (Ben) calls EVERY endpoint that works with projects or bugs, using the real ids
//     of Ana's organization, and gets nothing.
//  2. That list of endpoints is compared with the endpoints the app really has. If someone adds
//     an endpoint and forgets about this list, test 2 fails and says which one: that is the
//     moment to decide who may use it and add it here.
public class EveryEndpointIsIsolatedTests : IsolationTestBase
{
    // Every endpoint of projects and bugs. {org}, {project} and {bug} are Ana's ids.
    public static TheoryData<string> DataEndpoints => new()
    {
        "GET /api/organizations/{org}/projects",
        "POST /api/organizations/{org}/projects",
        "GET /api/projects/{project}",
        "PUT /api/projects/{project}",
        "DELETE /api/projects/{project}",
        "GET /api/projects/{project}/bugs",
        "POST /api/projects/{project}/bugs",
        "DELETE /api/projects/{project}/bugs",
        "GET /api/bugs",
        "GET /api/bugs/{bug}",
        "PUT /api/bugs/{bug}",
        "PATCH /api/bugs/{bug}",
        "DELETE /api/bugs/{bug}"
    };

    [Theory]
    [MemberData(nameof(DataEndpoints))]
    public async Task AnOutsider_GetsNothingFromAnyEndpoint(string endpoint)
    {
        await SignUpAnaAndBen();
        var project = await CreateProject(Ana, Ana.OrganizationId);
        var bug = await CreateBug(Ana, project.Id);

        var (method, template) = Split(endpoint);
        var path = template
            .Replace("{org}", Ana.OrganizationId.ToString())
            .Replace("{project}", project.Id.ToString())
            .Replace("{bug}", bug.Id.ToString());

        // A body that would be perfectly valid, so a refusal can only be about who is asking
        var response = await Send(Ben, new HttpMethod(method), path, new
        {
            name = "Intruder",
            code = "INT",
            projectId = project.Id,
            title = "Intruder bug",
            description = "Written by someone outside the organization.",
            severity = "Low",
            status = "Closed"
        });

        if (endpoint == "GET /api/bugs")
        {
            // The search across projects answers 200 for everybody, but only with their own bugs
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(bug.Title, json);
            return;
        }

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // And Ana's side is exactly as she left it
        Assert.Equal(bug.Title, (await GetBug(Ana, bug.Id)).Title);
        var projectAfter = await Send(Ana, HttpMethod.Get, $"/api/projects/{project.Id}");
        Assert.Equal("Pink in the Night", (await projectAfter.Content.ReadFromJsonAsync<ProjectDto>())!.Name);
    }

    [Fact]
    public void TheListAbove_IsEveryEndpointOfProjectsAndBugs_NoMoreNoLess()
    {
        var real = Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => !Normalize(endpoint.RoutePattern).StartsWith("api/auth/"))
            .SelectMany(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods
                .Select(method => $"{method} {Normalize(endpoint.RoutePattern)}"))
            .Order()
            .ToList();

        var listed = DataEndpoints
            .Select(row => (string)row[0])
            .Select(Split)
            .Select(endpoint => $"{endpoint.Method} {Normalize(endpoint.Template.TrimStart('/'))}")
            .Order()
            .ToList();

        // If this fails, an endpoint was added (or removed). Decide who may use it, add it to
        // DataEndpoints with a test that an outsider gets nothing, and then it passes again.
        Assert.Equal(listed, real);
    }

    private static (string Method, string Template) Split(string endpoint)
    {
        var parts = endpoint.Split(' ', 2);
        return (parts[0], parts[1]);
    }

    private static string Normalize(RoutePattern pattern) => Normalize(pattern.RawText!);

    // Same endpoint, however it is written: lowercase, and every {parameter} just "{}"
    private static string Normalize(string route)
        => System.Text.RegularExpressions.Regex.Replace(route.ToLowerInvariant(), @"\{[^}]*\}", "{}");
}
