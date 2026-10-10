using System.ComponentModel.DataAnnotations;
using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.DTOs
{
    public abstract class ProjectRequestDto
    {
        [Required]
        [MinLength(2)]
        [MaxLength(Project.NameMaxLength)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(Project.DescriptionMaxLength)]
        public string? Description { get; set; }

        // Lowercase is accepted here ("pin") and stored in uppercase ("PIN") by the controller
        [Required]
        [RegularExpression(
            "^[A-Za-z]{3}$",
            ErrorMessage = "The code must be exactly 3 letters (no digits or symbols).")]
        public string Code { get; set; } = string.Empty;
    }
}
