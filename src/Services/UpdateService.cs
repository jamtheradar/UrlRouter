using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using UrlRouter.Models;

namespace UrlRouter.Services
{
    /// <summary>One entry in the published update feed.</summary>
    public sealed class UpdateManifest
    {
        /// <summary>Four-part version of the release, e.g. "2026.8.28.1615".</summary>
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        /// <summary>Direct download URL of that release's UrlRouter.exe.</summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        /// <summary>Lower-case hex SHA-256 of the file at <see cref="Url"/>.</summary>
        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        /// <summary>Human-readable release notes shown in the confirmation prompt.</summary>
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        /// <summary>Below this version the update cannot be skipped. Optional.</summary>
        [JsonPropertyName("minRequiredVersion")]
        public string? MinRequiredVersion { get; set; }

        /// <summary>Page to open when the user would rather install by hand.</summary>
        [JsonPropertyName("releasePage")]
        public string? ReleasePage { get; set; }
    }

    /// <summary>Outcome of one poll of the feed.</summary>
    /// <param name="Current">Version of the running executable.</param>
    /// <param name="Manifest">What the feed advertised, or null if it could not be read.</param>
    /// <param name="IsNewer">True when the manifest is genuinely ahead of the running version.</param>
    /// <param name="IsRequired">True when the running version is below the feed's minRequiredVersion.</param>
    /// <param name="Error">Why the check failed, for the Updates tab. Null on success.</param>
    public sealed record UpdateCheckResult(
        Version Current,
        UpdateManifest? Manifest,
        bool IsNewer,
        bool IsRequired,
        string? Error)
    {
        /// <summary>Parsed feed version, or null when the feed was unreadable or malformed.</summary>
        public Version? Available =>
            Version.TryParse(Manifest?.Version, out var v) ? v : null;
    }

    /// <summary>How far <see cref="UpdateService.ApplyStaged"/> got.</summary>
    public enum UpdateApplyResult
    {
        /// <summary>
        /// Nothing was installed and the running build is genuinely untouched - either the swap
        /// never started, or it was rolled back. The only outcome the user may be told that about.
        /// </summary>
        Failed,

        /// <summary>Installed, and the successor is starting. The caller should now exit.</summary>
        Installed,

        /// <summary>
        /// Installed, but no successor could be started. The caller must NOT exit: this process
        /// is the only agent left, and there is nothing queued to replace it before sign-in.
        /// </summary>
        InstalledPendingRestart,
    }

    /// <summary>
    /// Polls the GitHub release feed and, on confirmation, replaces this executable in place.
    ///
    /// Nothing here is ever touched by the routing hot path - only the resident agent and the
    /// settings window call in - because a link click must not pay to load HttpClient.
    ///
    /// Replacement works because the app is published as a single file to a fixed directory:
    /// Windows will let a running image be *renamed* even though it cannot be overwritten, so
    /// the swap is two renames and the registered path is never absent or stale.
    /// </summary>
    public static class UpdateService
    {
        /// <summary>
        /// The published feed, from <see cref="RouterConfig.DefaultUpdateFeedUrl"/>.
        ///
        /// It is deliberately the `releases/latest/download` alias rather than the GitHub API:
        /// it needs no token, is not subject to the API's 60-per-hour per-IP limit (an office
        /// behind one NAT shares that), and is less often blocked by a corporate proxy than
        /// api.github.com. Drafts and prereleases do not resolve through it, which is what makes
        /// a draft release a safe dry run of a publish.
        /// </summary>
        public const string DefaultFeedUrl = RouterConfig.DefaultUpdateFeedUrl;

        /// <summary>Name the freshly downloaded executable is staged under, beside the live one.</summary>
        private const string StagedFileName = "UrlRouter.new.exe";

        /// <summary>Name the outgoing executable is renamed to. Deleted on the next agent start.</summary>
        private const string RetiredFileName = "UrlRouter.old.exe";

        /// <summary>Held for the whole download-and-swap. See <see cref="TryAcquireInstallLock"/>.</summary>
        private const string LockFileName = "UrlRouter.update.lock";

        /// <summary>Don't re-poll more often than this, however often the agent restarts.</summary>
        public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(20);

        private static readonly Lazy<HttpClient> Client = new(CreateClient);

        // ------------------------------------------------------------------ version

        /// <summary>
        /// FileVersion of the running executable. Read from <see cref="Environment.ProcessPath"/>
        /// rather than Assembly.Location, which is an empty string in a single-file publish.
        /// </summary>
        public static Version CurrentVersion
        {
            get
            {
                try
                {
                    var path = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(path))
                    {
                        var info = FileVersionInfo.GetVersionInfo(path);
                        if (Version.TryParse(info.FileVersion, out var fromFile))
                            return fromFile;
                    }
                }
                catch (Exception)
                {
                    // Fall through to the assembly's own version.
                }

                return typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
            }
        }

        // ------------------------------------------------------------------ checking

        /// <summary>
        /// True when the cooldown has elapsed and checking is switched on. The agent asks this
        /// before polling, so a machine restarted ten times in a morning still checks once.
        /// </summary>
        public static bool IsCheckDue(RouterConfig config) =>
            config.UpdateCheckEnabled &&
            DateTime.UtcNow - config.UpdateLastCheckUtc >= CheckInterval;

        /// <summary>
        /// Fetches the feed and compares it against the running version. Never throws: an
        /// unreachable, blocked or malformed feed is reported through
        /// <see cref="UpdateCheckResult.Error"/> and must not disturb link routing.
        /// </summary>
        public static async Task<UpdateCheckResult> CheckAsync(RouterConfig config, CancellationToken ct = default)
        {
            var current = CurrentVersion;
            var url = string.IsNullOrWhiteSpace(config.UpdateFeedUrl)
                ? DefaultFeedUrl
                : config.UpdateFeedUrl;

            try
            {
                using var response = await Client.Value.GetAsync(url, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return Failed(current, $"the feed returned {(int)response.StatusCode} {response.ReasonPhrase}");
                }

                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                // Strip a UTF-8 BOM. Most Windows tooling writes one by default (Set-Content
                // -Encoding utf8 on PowerShell 5.1, Notepad), and System.Text.Json reads the
                // U+FEFF as an unexpected character and throws - which would reach the user as
                // the update check simply never finding anything again.
                json = json.TrimStart('﻿');

                var manifest = JsonSerializer.Deserialize<UpdateManifest>(json);
                if (manifest is null || !Version.TryParse(manifest.Version, out var available))
                {
                    return Failed(current, "the feed did not contain a usable version");
                }

                var isRequired =
                    Version.TryParse(manifest.MinRequiredVersion, out var minimum) && current < minimum;

                return new UpdateCheckResult(current, manifest, available > current, isRequired, null);
            }
            catch (Exception ex)
            {
                return Failed(current, ex.Message);
            }
        }

        private static UpdateCheckResult Failed(Version current, string error)
        {
            RouterLog.Write($"[update] check failed: {error}");
            return new UpdateCheckResult(current, null, false, false, error);
        }

        /// <summary>
        /// Whether this result should be put in front of the user. A version the user has
        /// already dismissed stays dismissed unless the feed marks the update as required.
        /// </summary>
        public static bool ShouldNotify(UpdateCheckResult result, RouterConfig config)
        {
            if (!result.IsNewer || result.Available is null) return false;
            if (result.IsRequired) return true;

            return !(Version.TryParse(config.UpdateSkippedVersion, out var skipped) &&
                     skipped >= result.Available);
        }

        // ------------------------------------------------------------------ applying

        /// <summary>
        /// Claims the right to run an install, or returns null if another one is already in
        /// flight. Held across the whole download-and-swap; dispose it when finished.
        ///
        /// This has to work between *processes*, not just within one: the resident agent and a
        /// standalone `--config` window can both reach the install path independently, and they
        /// share one staging filename in one directory. It is a lock file rather than a named
        /// mutex because a Mutex is thread-affine and this lock is held across awaits.
        /// </summary>
        public static IDisposable? TryAcquireInstallLock()
        {
            if (InstallDirectory is not { } directory) return null;

            try
            {
                // DeleteOnClose so a crash mid-install cannot wedge every future one.
                return new FileStream(
                    Path.Combine(directory, LockFileName),
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
                    bufferSize: 1, FileOptions.DeleteOnClose);
            }
            catch (IOException)
            {
                // Someone else holds it. That is the answer, not an error.
                return null;
            }
            catch (Exception ex)
            {
                RouterLog.Write($"[update] could not take the install lock: {ex.Message}");
                return null;
            }
        }

        /// <summary>Directory the running executable lives in, or null if it cannot be resolved.</summary>
        public static string? InstallDirectory =>
            Environment.ProcessPath is { Length: > 0 } path ? Path.GetDirectoryName(path) : null;

        /// <summary>
        /// Downloads the release named by the manifest into the install directory and verifies
        /// its SHA-256, returning the staged path.
        ///
        /// The file is *created* in the install directory rather than downloaded to %TEMP% and
        /// moved in, because a moved file keeps the ACL it was created with: created here it
        /// inherits the directory's ALL APPLICATION PACKAGES ACEs, so the new executable is
        /// launchable by the new Outlook and Teams the moment it is swapped in.
        /// </summary>
        /// <exception cref="InvalidOperationException">The download was incomplete or its hash did not match.</exception>
        public static async Task<string> DownloadAsync(
            UpdateManifest manifest,
            IProgress<int>? progress = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(manifest.Url))
                throw new InvalidOperationException("The feed did not name a download URL.");

            var directory = InstallDirectory
                ?? throw new InvalidOperationException("Cannot resolve this application's own directory.");

            var staged = Path.Combine(directory, StagedFileName);

            // A previous attempt that died mid-download would otherwise be appended to.
            TryDelete(staged);

            using (var response = await Client.Value
                .GetAsync(manifest.Url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? 0;
                var written = 0L;

                await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var destination = new FileStream(
                    staged, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);

                var buffer = new byte[64 * 1024];
                int read;
                while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    written += read;

                    if (total > 0)
                        progress?.Report((int)(written * 100 / total));
                }
            }

            // The hash travels in the same manifest over the same TLS connection, so this proves
            // the download arrived intact - not that it is authentic. Authenticity rests on the
            // release itself; see the security note in README.md.
            if (!string.IsNullOrWhiteSpace(manifest.Sha256))
            {
                var actual = ComputeSha256(staged);
                if (!actual.Equals(manifest.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(staged);
                    throw new InvalidOperationException(
                        $"The download did not match the published checksum (expected {manifest.Sha256}, got {actual}).");
                }
            }

            RouterLog.Write($"[update] staged {manifest.Version} at {staged}");
            return staged;
        }

        /// <summary>
        /// Swaps the staged executable in and starts its successor.
        ///
        /// The running image cannot be overwritten, but Windows will rename it, so this is two
        /// renames rather than a copy: the registered path therefore never points at a missing
        /// file, even if the machine dies between them. The successor is started with --wait, so
        /// it sits on the single-instance mutex until the outgoing agent has let go of it.
        ///
        /// The three outcomes are distinct on purpose. Once the renames succeed the new build is
        /// installed whether or not anything else works, and reporting that as a plain failure
        /// would tell the user their old version is untouched when it is already gone.
        /// </summary>
        /// <param name="stagedPath">The verified download, from <see cref="DownloadAsync"/>.</param>
        /// <param name="expectedSha256">
        /// Re-checked immediately before the swap. The staged path is a fixed name in a shared
        /// directory, so between the download's own check and this moment another process could
        /// in principle have replaced the file - and this is the last point at which those bytes
        /// are still just a file rather than the registered browser handler.
        /// </param>
        public static UpdateApplyResult ApplyStaged(string stagedPath, string? expectedSha256 = null)
        {
            var live = Environment.ProcessPath;
            if (string.IsNullOrEmpty(live) || !File.Exists(stagedPath))
                return UpdateApplyResult.Failed;

            var directory = Path.GetDirectoryName(live)!;
            var retired = Path.Combine(directory, RetiredFileName);

            try
            {
                if (!string.IsNullOrWhiteSpace(expectedSha256))
                {
                    var actual = ComputeSha256(stagedPath);
                    if (!actual.Equals(expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        TryDelete(stagedPath);
                        RouterLog.Write("[update] staged file changed after verification; refusing to install");
                        return UpdateApplyResult.Failed;
                    }
                }

                // A retired copy from a previous update is still locked if anything is running
                // from it; deleting it is best-effort and its failure must not block this one.
                TryDelete(retired);

                File.Move(live, retired);
                try
                {
                    File.Move(stagedPath, live);
                }
                catch (Exception)
                {
                    // Put the working executable back rather than leaving the registered path
                    // pointing at nothing.
                    File.Move(retired, live);
                    throw;
                }
            }
            catch (Exception ex)
            {
                // Nothing moved, or the rollback above put it back. The old build is genuinely
                // still in place, which is the only case the caller may describe that way.
                RouterLog.Write($"[update] could not apply: {ex.Message}");
                return UpdateApplyResult.Failed;
            }

            // Past this point the new executable IS the registered handler. Anything that fails
            // from here is a restart problem, not an install problem, and rolling the files back
            // would throw away a good build to fix a symptom.
            try
            {
                // A file moved within a directory keeps the ACL it was created with, which is the
                // right one here - but re-asserting costs nothing and covers a staged file that
                // arrived by some other route.
                RegistrationService.GrantAppContainerAccess(live);

                // Never UseShellExecute here: the shell resolves this executable through our own
                // http association, and starting it that way is how a routing loop begins.
                Process.Start(new ProcessStartInfo(live)
                {
                    Arguments = "--agent --wait",
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                });

                RouterLog.Write($"[update] applied; restarting as {live}");
                return UpdateApplyResult.Installed;
            }
            catch (Exception ex)
            {
                // Installed, but nothing is queued to take over. The caller must NOT exit on
                // this: doing so would leave the machine with no agent at all, and so with dead
                // Outlook links, until the next sign-in fires the Run entry.
                RouterLog.Write($"[update] installed but could not start the successor: {ex.Message}");
                return UpdateApplyResult.InstalledPendingRestart;
            }
        }

        /// <summary>
        /// Removes the executable left behind by the last update. Called on agent start, which is
        /// the first moment the old image is guaranteed to be unloaded.
        /// </summary>
        public static void CleanupRetired()
        {
            if (InstallDirectory is not { } directory) return;

            TryDelete(Path.Combine(directory, RetiredFileName));
            TryDelete(Path.Combine(directory, StagedFileName));
        }

        // ------------------------------------------------------------------ helpers

        public static string ComputeSha256(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception)
            {
                // Still in use, or gone already. Either way there is nothing useful to do.
            }
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"UrlRouter/{CurrentVersion}");
            return client;
        }
    }
}
