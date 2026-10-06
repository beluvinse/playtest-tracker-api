using PlaytestTracker.Api.DTOs;

namespace PlaytestTracker.Api.Models
{
    public enum BugOperationStatus
    {
        Success,
        BugNotFound,
        ProjectNotFound
    }

    public class BugOperationResult
    {
        public BugOperationStatus Status { get; set; }
        public BugDto? Bug { get; set; }
    }
}
