namespace UrlRouter.Models
{
    /// <summary>
    /// Everything persisted to %APPDATA%\UrlRouter\config.json.
    /// </summary>
    public class RouterConfig
    {
        /// <summary>Browsers/profiles a rule can point at.</summary>
        public List<BrowserTarget> Targets { get; set; } = new();

        /// <summary>Evaluated in order; first enabled match wins.</summary>
        public List<RoutingRule> Rules { get; set; } = new();

        /// <summary>
        /// Target used when no rule matches. Null (the default) means "ask" - the picker
        /// is shown instead of guessing.
        /// </summary>
        public string? FallbackTargetId { get; set; }

        /// <summary>Append every routing decision to router.log. On by default; it is size-capped.</summary>
        public bool LogEnabled { get; set; } = true;

        /// <summary>
        /// Unwrap Outlook/Teams Safe Links before matching. Effectively required - without it
        /// every emailed link matches the safelinks host rather than its real destination.
        /// </summary>
        public bool UnwrapSafeLinks { get; set; } = true;

        // ------------------------------------------------------------------ updates

        /// <summary>
        /// Where <c>UpdateService</c> polls for new releases. Stored rather than compiled in so a
        /// fork, or an internal mirror behind a proxy that blocks github.com, needs no rebuild.
        /// </summary>
        public string UpdateFeedUrl { get; set; } = DefaultUpdateFeedUrl;

        /// <summary>
        /// Poll for new releases in the background. On by default; the agent only ever notifies,
        /// and nothing is downloaded or installed without the user saying yes.
        /// </summary>
        public bool UpdateCheckEnabled { get; set; } = true;

        /// <summary>Last successful poll, used to hold the check to roughly once a day.</summary>
        public DateTime UpdateLastCheckUtc { get; set; } = DateTime.MinValue;

        /// <summary>Version the user chose to skip; anything at or below it stops prompting.</summary>
        public string? UpdateSkippedVersion { get; set; }

        /// <summary>
        /// Default feed. Lives here rather than in the service so the serialised config carries a
        /// real URL a reader can see and change, instead of a null that means "the built-in one".
        /// </summary>
        public const string DefaultUpdateFeedUrl =
            "https://github.com/JamTheRadar/UrlRouter/releases/latest/download/version.json";

        public BrowserTarget? FindTarget(string? id) =>
            string.IsNullOrEmpty(id) ? null : Targets.FirstOrDefault(t => t.Id == id);
    }
}
