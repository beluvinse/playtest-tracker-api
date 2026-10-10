using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PlaytestTracker.Api.Options;
using PlaytestTracker.Api.Services;

namespace PlaytestTracker.Api.Extensions
{
    public static class AuthenticationServiceExtensions
    {
        // Teaches the API to read the "Authorization: Bearer <token>" header and check the token.
        // Checking a token needs no database: the signature, expiration, issuer and audience
        // are enough to trust what the token says.
        public static IServiceCollection AddAppAuthentication(this IServiceCollection services)
        {
            // Reads the "Jwt" section and refuses to start the app if something is wrong or missing
            // (no key, key too short…), instead of failing on the first login
            services.AddOptions<JwtOptions>()
                .BindConfiguration(JwtOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton(TimeProvider.System);
            services.AddScoped<TokenService>();

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer();

            // The bearer settings need the Jwt options, which are only known once the app is built,
            // so they are filled in here and not inside AddJwtBearer(...)
            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
                {
                    var jwt = jwtOptions.Value;

                    // Keep claim names as they are in the token ("sub" stays "sub")
                    bearer.MapInboundClaims = false;

                    bearer.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = TokenService.CreateKey(jwt),
                        ValidateIssuer = true,
                        ValidIssuer = jwt.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwt.Audience,
                        ValidateLifetime = true,
                        // By default a token stays valid 5 extra minutes after it expires
                        ClockSkew = TimeSpan.FromSeconds(30),
                        NameClaimType = "name"
                    };
                });

            return services;
        }
    }
}
