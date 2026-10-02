using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.Services
{
    public class BugService
    {
        private readonly List<BugReport> _bugs = new()
    {
        new BugReport
        {
            Id = 1,
            Title = "Map UI is cropped",
            Description = "The map is cropped at 2560x1600.",
            Severity = Severity.High,
            Status = BugStatus.Open,
            CreatedAt = DateTime.Now
        },
        new BugReport
        {
            Id = 2,
            Title = "Pause menu overlaps map",
            Description = "Opening pause while viewing the map causes UI overlap.",
            Severity = Severity.Medium,
            Status = BugStatus.InProgress,
            CreatedAt = DateTime.Now.AddMinutes(-30)
        }
    };

        public List<BugReport> GetAll(BugStatus? status = null, Severity? severity = null, string? search = null)
        {
            var query = _bugs.AsEnumerable();

            if (status.HasValue)
            {
                query = query.Where(bug => bug.Status == status.Value);
            }

            if (severity.HasValue)
            {
                query = query.Where(bug => bug.Severity == severity.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(bug =>
                    bug.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    bug.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            return query.ToList();
        }

        public BugReport? GetById(int id)
        {
            return _bugs.FirstOrDefault(bug => bug.Id == id);
        }

        public BugReport Add(BugReport bug)
        {
            bug.Id = _bugs.Max(b => b.Id) + 1;
            bug.Status = BugStatus.Open;
            bug.CreatedAt = DateTime.Now;

            _bugs.Add(bug);

            return bug;
        }

        public BugReport? Update(int id, BugReport updatedBug)
        {
            var bug = _bugs.FirstOrDefault(b => b.Id == id);

            if (bug == null)
            {
                return null;
            }

            bug.Title = updatedBug.Title;
            bug.Description = updatedBug.Description;
            bug.Severity = updatedBug.Severity;
            bug.Status = updatedBug.Status;

            return bug;
        }

        public bool Delete(int id)
        {
            var bug = _bugs.FirstOrDefault(b => b.Id == id);

            if (bug == null)
            {
                return false;
            }

            _bugs.Remove(bug);

            return true;
        }
    }
}
