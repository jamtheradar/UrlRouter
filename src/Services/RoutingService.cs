using UrlRouter.Forms;

namespace UrlRouter.Services
{
    /// <summary>
    /// The routing decision itself, shared by the two ways a URL can arrive: launched as a
    /// process by the shell, or handed to the resident agent over DDE.
    /// </summary>
    public static class RoutingService
    {
        /// <summary>
        /// Normalizes, matches and opens a URL. Assumes WinForms has been initialised,
        /// because an unmatched URL raises the picker.
        /// </summary>
        /// <param name="source">How the URL reached us, recorded in the log.</param>
        /// <returns>True when a browser was launched.</returns>
        public static bool Route(string rawUrl, string source)
        {
            // Reloaded per URL rather than cached, so rules added from the picker take
            // effect immediately even in a long-lived agent.
            var config = ConfigService.Load();
            RouterLog.Enabled = config.LogEnabled;

            var normalized = UrlNormalizer.Normalize(rawUrl, config.UnwrapSafeLinks);

            if (!Uri.TryCreate(normalized.Normalized, UriKind.Absolute, out var uri))
            {
                RouterLog.Write($"[{source}] UNROUTABLE  {rawUrl}");
                MessageBox.Show($"Could not understand this link:\n\n{rawUrl}",
                    "URL Router", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            var match = RuleMatcher.Match(config, uri);
            var target = match.Target;

            var decision = target is null
                ? "picker"
                : match.Rule is not null
                    ? $"rule '{match.Rule.HostPattern}' -> {target.DisplayName}"
                    : $"fallback -> {target.DisplayName}";

            RouterLog.Write(normalized.WasWrapped
                ? $"[{source}] {decision}  |  {normalized.Normalized}  (unwrapped)"
                : $"[{source}] {decision}  |  {normalized.Normalized}");

            if (target is null)
            {
                target = PickerForm.Choose(config, uri, normalized.Normalized);
                if (target is null)
                {
                    RouterLog.Write($"[{source}] cancelled at picker");
                    return false;
                }
            }

            if (BrowserLauncher.TryLaunch(target, normalized.Normalized, out var error))
            {
                return true;
            }

            RouterLog.Write($"[{source}] LAUNCH FAILED  {target.DisplayName}: {error}");
            MessageBox.Show($"Could not open {target.DisplayName}.\n\n{error}\n\nURL: {normalized.Normalized}",
                "URL Router", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }
}
