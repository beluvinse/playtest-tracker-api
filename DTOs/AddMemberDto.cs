using System.ComponentModel.DataAnnotations;

namespace PlaytestTracker.Api.DTOs
{
    // Adding someone to an organization: by the email of their account. There are no invitations
    // (that would need sending emails), so the person has to have signed up already.
    public class AddMemberDto
    {
        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;
    }
}
