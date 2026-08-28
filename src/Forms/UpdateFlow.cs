using System.Diagnostics;
using UrlRouter.Models;
using UrlRouter.Services;

namespace UrlRouter.Forms
{
    /// <summary>What the user chose when offered an update.</summary>
    public enum UpdateOutcome
    {
        /// <summary>Nothing to offer, or the user asked to be reminded later.</summary>
        Dismissed,

        /// <summary>The user skipped this version; it will not be offered again.</summary>
        Skipped,

        /// <summary>The download or the swap failed. The running version is untouched.</summary>
        Failed,

        /// <summary>The new executable is in place and its successor has been started.</summary>
        Applied,

        /// <summary>
        /// The new executable is in place but nothing was started to take over. The caller must
        /// keep running and tell the user a restart is still owed - exiting on this would leave
        /// the machine with no agent, and so with dead Outlook links, until the next sign-in.
        /// </summary>
        InstalledPendingRestart,
    }

    /// <summary>
    /// The confirm-download-install conversation, shared by the tray icon and the Updates tab so
    /// that the two cannot drift into offering different things.
    ///
    /// Nothing is downloaded before the user says yes. That is the whole reason this exists as a
    /// prompt rather than a silent updater: the app replaces its own executable, and doing that
    /// unannounced under a link handler people depend on is not a decision to take for them.
    /// </summary>
    public static class UpdateFlow
    {
        /// <summary>
        /// Offers the update, and on confirmation downloads, verifies and swaps it in.
        /// </summary>
        /// <param name="owner">Window to centre the dialogs on, or null.</param>
        /// <param name="result">A check whose <see cref="UpdateCheckResult.IsNewer"/> is true.</param>
        /// <param name="config">Saved when the user skips a version, so the choice sticks.</param>
        /// <param name="report">Optional progress sink for a status label. Called on the UI thread.</param>
        public static async Task<UpdateOutcome> OfferAsync(
            IWin32Window? owner,
            UpdateCheckResult result,
            RouterConfig config,
            Action<string>? report = null)
        {
            if (result.Manifest is not { } manifest || result.Available is not { } available)
                return UpdateOutcome.Dismissed;

            var choice = Ask(owner, result, available);

            if (choice == DialogResult.No)
            {
                config.UpdateSkippedVersion = available.ToString();
                TrySaveConfig(config);
                RouterLog.Write($"[update] {available} skipped by user");
                return UpdateOutcome.Skipped;
            }

            if (choice != DialogResult.Yes)
                return UpdateOutcome.Dismissed;

            // One install at a time, across processes: the agent and a standalone --config window
            // can both get here, and they share one staging file in one directory.
            using var installLock = UpdateService.TryAcquireInstallLock();
            if (installLock is null)
            {
                ShowError(owner, "Another copy of URL Router is already installing an update. " +
                                 "Wait for it to finish and try again.");
                return UpdateOutcome.Failed;
            }

            try
            {
                report?.Invoke($"Downloading {available}…");

                var progress = new Progress<int>(percent => report?.Invoke($"Downloading {available}… {percent}%"));
                var staged = await UpdateService.DownloadAsync(manifest, progress).ConfigureAwait(true);

                report?.Invoke("Installing…");

                switch (UpdateService.ApplyStaged(staged, manifest.Sha256))
                {
                    case UpdateApplyResult.Installed:
                        return UpdateOutcome.Applied;

                    // Installed, but nothing is queued to take over. Saying "untouched" here would
                    // be false, and returning Applied would make the caller exit and leave the
                    // machine with no agent - so this is its own outcome.
                    case UpdateApplyResult.InstalledPendingRestart:
                        return UpdateOutcome.InstalledPendingRestart;

                    default:
                        ShowError(owner, "The update was downloaded but could not be installed. " +
                                         "The existing version is untouched — see router.log for details.");
                        return UpdateOutcome.Failed;
                }
            }
            catch (Exception ex)
            {
                RouterLog.Write($"[update] install failed: {ex.Message}");
                ShowError(owner, $"The update could not be installed:\n\n{ex.Message}");
                return UpdateOutcome.Failed;
            }
        }

        private static DialogResult Ask(IWin32Window? owner, UpdateCheckResult result, Version available)
        {
            var notes = string.IsNullOrWhiteSpace(result.Manifest?.Notes)
                ? string.Empty
                : $"\n\n{Truncate(result.Manifest!.Notes!, 600)}";

            // A required update has no "skip" outcome to offer, so it gets the two-button prompt
            // rather than a Skip the user would reasonably expect to be honoured and which the
            // feed has already overruled.
            if (result.IsRequired)
            {
                return MessageBox.Show(
                    owner,
                    $"URL Router {available} is a required update (you have {result.Current}).{notes}\n\n" +
                    "Install it now? The agent will restart; links keep working.",
                    "URL Router — update required",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK
                        ? DialogResult.Yes
                        : DialogResult.Cancel;
            }

            return MessageBox.Show(
                owner,
                $"URL Router {available} is available (you have {result.Current}).{notes}\n\n" +
                "Install it now? The agent will restart; links keep working.\n\n" +
                "Yes — install now\n" +
                "No — skip this version\n" +
                "Cancel — remind me later",
                "URL Router — update available",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
        }

        /// <summary>Opens the release page for anyone who would rather install by hand.</summary>
        public static void OpenReleasePage(UpdateManifest? manifest)
        {
            var url = manifest?.ReleasePage;
            if (string.IsNullOrWhiteSpace(url)) return;

            try
            {
                // Deliberately UseShellExecute, and deliberately safe: this is an ordinary link,
                // so letting it route through URL Router like any other is the correct behaviour.
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                RouterLog.Write($"[update] could not open release page: {ex.Message}");
            }
        }

        private static void TrySaveConfig(RouterConfig config)
        {
            try
            {
                ConfigService.Save(config);
            }
            catch (Exception ex)
            {
                RouterLog.Write($"[update] could not save update preferences: {ex.Message}");
            }
        }

        private static void ShowError(IWin32Window? owner, string message) =>
            MessageBox.Show(owner, message, "URL Router", MessageBoxButtons.OK, MessageBoxIcon.Error);

        private static string Truncate(string text, int max) =>
            text.Length <= max ? text : text[..max] + "…";
    }
}
