using PlaytestTracker.Api.DTOs;

namespace PlaytestTracker.Api.Models
{
    public enum ProjectOperationStatus
    {
        Success,
        ProjectNotFound,
        OrganizationNotFound,
        CodeTaken
    }

    public class ProjectOperationResult
    {
        public ProjectOperationStatus Status { get; set; }
        public ProjectDto? Project { get; set; }
    }
}
