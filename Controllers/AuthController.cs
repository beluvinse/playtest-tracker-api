using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using PlaytestTracker.Api.DTOs;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Services;

namespace PlaytestTracker.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly TokenService _tokenService;

        public AuthController(AuthService authService, TokenService tokenService)
        {
            _authService = authService;
            _tokenService = tokenService;
        }

        // Creates the account and signs the person in right away, so they get a token
        // without having to log in a second time
        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto)
        {
            var result = await _authService.RegisterAsync(
                dto.Email.Trim(), dto.Password, dto.DisplayName.Trim());

            switch (result.Status)
            {
                case RegisterStatus.EmailTaken:
                    return FieldProblem(StatusCodes.Status409Conflict, "Email already in use",
                        nameof(RegisterDto.Email), "There is already an account with this email.");

                case RegisterStatus.InvalidEmail:
                    return FieldProblem(StatusCodes.Status400BadRequest, "Invalid email",
                        nameof(RegisterDto.Email), "This is not a valid email address.");

                case RegisterStatus.InvalidPassword:
                    foreach (var error in result.PasswordErrors)
                        ModelState.AddModelError(nameof(RegisterDto.Password), error);
                    return ValidationProblem(ModelState);
            }

            return Created("/api/auth/me", CreateAuthResponse(result.User!));
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
        {
            var result = await _authService.CheckCredentialsAsync(dto.Email.Trim(), dto.Password);

            return result.Status switch
            {
                LoginStatus.Success => Ok(CreateAuthResponse(result.User!)),

                // The same answer for a wrong password and for an email without an account
                LoginStatus.InvalidCredentials => Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Invalid email or password"),

                // 423 Locked: the account exists but is paused for a while
                _ => Problem(
                    statusCode: StatusCodes.Status423Locked,
                    title: "Account temporarily locked",
                    detail: LockedDetail(result.LockedUntil))
            };
        }

        // "Who am I?": the frontend calls this with a saved token to find out if it still works
        // and whose it is. [Authorize] answers 401 by itself when the token is missing or invalid.
        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<UserDto>> Me()
        {
            // "sub" is the person's id, put in the token by TokenService
            var id = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var user = id == null ? null : await _authService.FindByIdAsync(id);

            // A valid token for an account that no longer exists is not a valid session
            if (user == null)
                return Unauthorized();

            return Ok(ToUserDto(user));
        }

        private AuthResponseDto CreateAuthResponse(ApplicationUser user)
        {
            var token = _tokenService.CreateToken(user);

            return new AuthResponseDto
            {
                Token = token.Token,
                ExpiresAt = token.ExpiresAt,
                User = ToUserDto(user)
            };
        }

        private static UserDto ToUserDto(ApplicationUser user) => new()
        {
            Id = user.Id,
            Email = user.Email!,
            DisplayName = user.DisplayName
        };

        // An error attached to one field, in the same "errors" format as validation errors,
        // so a form can show the message right under that field
        private ActionResult FieldProblem(int statusCode, string title, string field, string message)
        {
            ModelState.AddModelError(field, message);

            return ValidationProblem(
                statusCode: statusCode,
                title: title,
                modelStateDictionary: ModelState);
        }

        private static string LockedDetail(DateTimeOffset? lockedUntil)
        {
            if (lockedUntil == null)
                return "Too many failed attempts. Try again later.";

            var minutes = (int)Math.Ceiling((lockedUntil.Value - DateTimeOffset.UtcNow).TotalMinutes);
            return $"Too many failed attempts. Try again in {Math.Max(minutes, 1)} minute(s).";
        }
    }
}
