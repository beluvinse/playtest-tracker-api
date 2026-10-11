using System.ComponentModel.DataAnnotations;

namespace PlaytestTracker.Api.Models
{
    // The "tenant": a group of people who share their projects and bugs. Nobody outside the
    // organization can see them. Think of a Slack workspace or a GitHub organization.
    public class Organization
    {
        public const int NameMaxLength = 100;

        public int Id { get; set; }

        [MaxLength(NameMaxLength)]
        public string Name { get; set; } = string.Empty;

        // The example organization anyone can look around in. It can't be deleted or have its
        // members changed, so it is always there for the next person who tries the app.
        public bool IsDemo { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public List<Membership> Members { get; set; } = new();
    }
}
