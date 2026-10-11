using Microsoft.OpenApi.Models;

namespace PlaytestTracker.Api.Extensions
{
    public static class SwaggerServiceExtensions
    {
        // Swagger UI (the test page at /swagger) with an "Authorize" button: paste the token you
        // got from login and it is sent as "Authorization: Bearer <token>" on every request,
        // so the protected endpoints can be tried from the page.
        public static IServiceCollection AddAppSwagger(this IServiceCollection services)
        {
            services.AddEndpointsApiExplorer();

            services.AddSwaggerGen(options =>
            {
                // Describes HOW this API is signed in to: an HTTP header with a bearer token
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "The token from POST /api/auth/login (just the token, without the word \"Bearer\")."
                });

                // Says that the endpoints use it. Swagger UI then adds the padlock to them and
                // sends the token on each request. (The two public endpoints, register and login,
                // ignore the header, so sending it there does no harm.)
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            return services;
        }
    }
}
