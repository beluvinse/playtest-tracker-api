using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.DTOs
{
    // What the sign-in page needs to offer "try the demo": who the demo accounts are and the
    // password they all share. This is PUBLIC on purpose: the demo accounts exist so anybody can
    // look around without signing up. Never put anything real in these accounts.
    public class DemoInfoDto
    {
        public string OrganizationName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public List<DemoAccountDto> Accounts { get; set; } = new();
    }

    public class DemoAccountDto
    {
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public OrganizationRole Role { get; set; }
    }
}
