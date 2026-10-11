using System.ComponentModel.DataAnnotations;
using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.DTOs
{
    // The body for creating an organization and for renaming one
    public class OrganizationRequestDto
    {
        [Required]
        [MinLength(2)]
        [MaxLength(Organization.NameMaxLength)]
        public string Name { get; set; } = string.Empty;
    }
}
