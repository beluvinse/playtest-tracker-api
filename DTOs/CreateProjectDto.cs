using System.ComponentModel.DataAnnotations;

namespace PlaytestTracker.Api.DTOs
{
    public class CreateProjectDto
    {
        [Required]
        [MinLength(2)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}
