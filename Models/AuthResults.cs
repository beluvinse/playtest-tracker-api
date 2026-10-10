namespace PlaytestTracker.Api.Models
{
    public enum RegisterStatus
    {
        Success,
        InvalidEmail,
        EmailTaken,
        InvalidPassword
    }

    public class RegisterResult
    {
        public RegisterStatus Status { get; set; }
        public ApplicationUser? User { get; set; }

        // For InvalidPassword: what is wrong with it, in words a person can act on
        public IReadOnlyList<string> PasswordErrors { get; set; } = [];
    }

    public enum LoginStatus
    {
        Success,

        // Wrong password, or no account with that email. Deliberately the same answer for both:
        // telling them apart would let anyone check which emails have an account here.
        InvalidCredentials,

        // Too many wrong passwords in a row: the account is paused for a while
        LockedOut
    }

    public class LoginResult
    {
        public LoginStatus Status { get; set; }
        public ApplicationUser? User { get; set; }

        // For LockedOut: when the account can be used again
        public DateTimeOffset? LockedUntil { get; set; }
    }
}
