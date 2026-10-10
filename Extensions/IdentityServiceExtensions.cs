using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.Extensions
{
    public static class IdentityServiceExtensions
    {
        // Identity: users, password hashing and lockout. AddIdentityCore is the small version
        // (no cookies and no built-in pages), because this API signs people in with tokens.
        //
        // It lives here, and not inline in Program.cs, so the tests can set up Identity with
        // exactly the same rules the API uses.
        public static IServiceCollection AddAppIdentity(this IServiceCollection services)
        {
            services
                .AddIdentityCore<ApplicationUser>(options =>
                {
                    // Length matters more than symbols: a long password is hard to guess, and easier to remember
                    options.Password.RequiredLength = 8;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequireUppercase = false;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireDigit = true;

                    // The email is how a person is identified, so two accounts can't share one
                    options.User.RequireUniqueEmail = true;

                    // 5 wrong passwords in a row lock the account for 10 minutes (slows down guessing)
                    options.Lockout.MaxFailedAccessAttempts = 5;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
                    options.Lockout.AllowedForNewUsers = true;
                })
                .AddEntityFrameworkStores<AppDbContext>();

            return services;
        }
    }
}
