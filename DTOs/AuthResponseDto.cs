namespace PlaytestTracker.Api.DTOs
{
    // The answer to a successful register or login: the token to send on every later request
    // ("Authorization: Bearer <token>"), when it stops working, and who it belongs to
    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        public UserDto User { get; set; } = new();
    }
}
