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
    }
}
