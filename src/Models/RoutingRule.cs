namespace UrlRouter.Models
{
    /// <summary>
    /// One entry in the ordered rule list. The first enabled rule whose patterns match the
    /// normalized URL decides the browser; see <see cref="Services.RuleMatcher"/>.
    /// </summary>
    public class RoutingRule
    {
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Host glob. "*" matches any run of characters, so "*.gmail.com" matches
        /// "sso.gmail.com" but not "gmail.com.example.org" (the pattern is anchored).
        /// Matched case-insensitively against the host only - no scheme, port or path.
        /// </summary>
        public string HostPattern { get; set; } = string.Empty;

        /// <summary>
        /// Optional path glob matched against the URL path, e.g. "/browse/ISD-*".
        /// If the pattern contains '?' the query string is included in the match target.
        /// Null or blank means "any path".
        /// </summary>
        public string? PathPattern { get; set; }

        /// <summary>Id of the <see cref="BrowserTarget"/> to open.</summary>
        public string TargetId { get; set; } = string.Empty;

        /// <summary>Free-text note shown in the config grid.</summary>
        public string? Comment { get; set; }
    }
}
