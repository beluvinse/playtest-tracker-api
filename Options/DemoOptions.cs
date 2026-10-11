namespace PlaytestTracker.Api.Options
{
    // The "Demo" section of the configuration: the example organization anybody can look around in.
    // Nothing here is secret: the demo accounts are public on purpose, so people can try the app.
    public class DemoOptions
    {
        public const string SectionName = "Demo";

        // Off by default, so nothing is created unless the configuration asks for it
        // (appsettings.json turns it on)
        public bool Enabled { get; set; }

        // Create the demo when the API starts. The tests turn this off and create it themselves,
        // once their database has its tables.
        public bool SeedOnStartup { get; set; } = true;

        // The password of every demo account. It has to meet the usual password rules.
        public string Password { get; set; } = string.Empty;
    }
}
