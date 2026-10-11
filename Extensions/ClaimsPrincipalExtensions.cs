using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace PlaytestTracker.Api.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        // The id of the person making the request, from the "sub" claim TokenService puts in the
        // token. Only call it in endpoints with [Authorize]: there, a valid token always has it.
        public static string GetUserId(this ClaimsPrincipal user)
            => user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
               ?? throw new InvalidOperationException("The token has no \"sub\" claim.");
    }
}
