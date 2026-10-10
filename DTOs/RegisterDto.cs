using System.ComponentModel.DataAnnotations;
using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.DTOs
{
    public class RegisterDto
    {
        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        // The strength rules (length, digit…) are checked by Identity. This maximum only stops
        // someone from sending a huge text, since hashing a password is deliberately slow work.
        [Required]
        [MaxLength(100)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [MinLength(2)]
        [MaxLength(ApplicationUser.DisplayNameMaxLength)]
        public string DisplayName { get; set; } = string.Empty;
    }
}
