namespace UrlRouter.Models
{
    /// <summary>
    /// A browser (optionally a specific profile within it) that a URL can be routed to.
    /// Detected automatically by <see cref="Services.BrowserDetectionService"/>, but every
    /// field is user-editable so arbitrary browsers can be added by hand.
    /// </summary>
    public class BrowserTarget
    {
        /// <summary>Stable identifier referenced by <see cref="RoutingRule.TargetId"/>.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Name shown in the picker and the rules grid, e.g. "Edge — Client1".</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Full path to the browser executable. Launched directly, never via ShellExecute.</summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// Chromium profile directory name (e.g. "Default", "Profile 1"). Emitted as
        /// --profile-directory="…". Leave blank for browsers that don't use that switch.
        /// </summary>
        public string? ProfileDirectory { get; set; }

        /// <summary>
        /// Extra command-line switches inserted before the URL, e.g. "-P work" for Firefox.
        /// Split on spaces except inside double quotes.
        /// </summary>
        public string? ExtraArguments { get; set; }

        /// <summary>Set on auto-detected targets so re-detection can update them safely.</summary>
        public bool AutoDetected { get; set; }

        public override string ToString() => DisplayName;
    }
}
