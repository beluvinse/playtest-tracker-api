using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using PlaytestTracker.Api.Tests.Support;
using Swashbuckle.AspNetCore.Swagger;

namespace PlaytestTracker.Api.Tests;

// The Swagger page itself only exists in Development, but the document that describes the API
// is built the same way everywhere, so it can be checked here.
public class SwaggerTests : IDisposable
{
    private readonly ApiFactory _factory = new ApiFactory().WithEmptyDatabase();

    public void Dispose() => _factory.Dispose();

    private OpenApiDocument Document() =>
        _factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");

    [Fact]
    public void TheDocumentDescribesBearerTokens_SoTheAuthorizeButtonExists()
    {
        var scheme = Document().Components.SecuritySchemes["Bearer"];

        Assert.Equal(SecuritySchemeType.Http, scheme.Type);
        Assert.Equal("bearer", scheme.Scheme);
        Assert.Equal("JWT", scheme.BearerFormat);
    }

    [Fact]
    public void TheWholeApiIsMarkedAsUsingIt()
    {
        // A requirement added with AddSecurityRequirement applies to the whole document, so it
        // is found at the top and not on each operation
        Assert.Contains(Document().SecurityRequirements,
            requirement => requirement.Keys.Any(scheme => scheme.Reference.Id == "Bearer"));
    }
}
