namespace PlaytestTracker.Api.Options
{
    // The "Maintenance" section of the configuration: the chores a scheduled job asks the API to do
    // (see MaintenanceController). Unlike the demo's, the Key IS secret: never put it in a file of
    // the repo. Set the Maintenance__Key environment variable.
    public class MaintenanceOptions
    {
        public const string SectionName = "Maintenance";

        // A key shorter than this is ignored, so a weak one can't be set by mistake
        public const int MinKeyLength = 32;

        // What the caller must send in the X-Maintenance-Key header. Without it (the default) the
        // maintenance endpoint doesn't exist as far as anybody can tell: it answers 404.
        public string Key { get; set; } = string.Empty;

        // Visitors who signed up more than this many days ago are deleted with their organizations
        public int VisitorMaxAgeDays { get; set; } = 7;
    }
}
