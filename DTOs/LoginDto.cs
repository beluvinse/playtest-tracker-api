using System.ComponentModel.DataAnnotations;

namespace PlaytestTracker.Api.DTOs
{
    public class LoginDto
    {
        [Required]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Password { get; set; } = string.Empty;
    }
}
