using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace PlaytestTracker.Api.Models
{
    // The person who logs in. IdentityUser already brings the fields every login system needs:
    // Id, Email, UserName, PasswordHash (never the password itself), lockout counters, etc.
    // Having our own class (even with one extra field) means adding more later, like an
    // organization, is a normal migration instead of a rewrite.
    public class ApplicationUser : IdentityUser
    {
        public const int DisplayNameMaxLength = 50;

        // What the app shows next to the person's activity ("belu"), instead of an email
        [MaxLength(DisplayNameMaxLength)]
        public string DisplayName { get; set; } = string.Empty;
    }
}
