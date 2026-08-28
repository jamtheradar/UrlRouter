using System.Text;
using System.Text.Json;
using UrlRouter.Models;
using Microsoft.Win32;

namespace UrlRouter.Services
{
    /// <summary>
    /// Discovers installed browsers and their profiles so targets do not have to be typed by hand.
    ///
    /// Chromium browsers record every profile in a JSON file called "Local State" under
    /// profile.info_cache, keyed by the profile *directory* name - which is exactly the value
    /// --profile-directory expects. Non-Chromium browsers are picked up from the shell's
    /// StartMenuInternet registration instead, as profile-less targets.
    /// </summary>
    public static class BrowserDetectionService
    {
        private record ChromiumBrowser(string Name, string UserDataRelativePath, string[] ExeCandidates);

        private static ChromiumBrowser[] KnownBrowsers()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            return new[]
            {
                new ChromiumBrowser("Edge",
                    Path.Combine(localAppData, @"Microsoft\Edge\User Data"),
                    new[]
                    {
                        Path.Combine(programFilesX86, @"Microsoft\Edge\Application\msedge.exe"),
                        Path.Combine(programFiles, @"Microsoft\Edge\Application\msedge.exe"),
                    }),
                new ChromiumBrowser("Brave",
                    Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data"),
                    new[]
                    {
                        Path.Combine(programFiles, @"BraveSoftware\Brave-Browser\Application\brave.exe"),
                        Path.Combine(programFilesX86, @"BraveSoftware\Brave-Browser\Application\brave.exe"),
                        Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\Application\brave.exe"),
                    }),
                new ChromiumBrowser("Chrome",
                    Path.Combine(localAppData, @"Google\Chrome\User Data"),
                    new[]
                    {
                        Path.Combine(programFiles, @"Google\Chrome\Application\chrome.exe"),
                        Path.Combine(programFilesX86, @"Google\Chrome\Application\chrome.exe"),
                        Path.Combine(localAppData, @"Google\Chrome\Application\chrome.exe"),
                    }),
                new ChromiumBrowser("Vivaldi",
                    Path.Combine(localAppData, @"Vivaldi\User Data"),
                    new[]
                    {
                        Path.Combine(localAppData, @"Vivaldi\Application\vivaldi.exe"),
                        Path.Combine(programFiles, @"Vivaldi\Application\vivaldi.exe"),
                    }),
            };
        }

        public static List<BrowserTarget> Detect()
        {
            var targets = new List<BrowserTarget>();
            var chromiumExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var browser in KnownBrowsers())
            {
                var exe = browser.ExeCandidates.FirstOrDefault(File.Exists);
                if (exe is null) continue;

                chromiumExes.Add(exe);

                var profiles = ReadProfiles(Path.Combine(browser.UserDataRelativePath, "Local State"));
                if (profiles.Count == 0)
                {
                    // Installed but never launched - still usable, just without profile switching.
                    targets.Add(new BrowserTarget
                    {
                        Id = Slug(browser.Name),
                        DisplayName = browser.Name,
                        ExecutablePath = exe,
                        AutoDetected = true,
                    });
                    continue;
                }

                var single = profiles.Count == 1;
                foreach (var profile in profiles)
                {
                    targets.Add(new BrowserTarget
                    {
                        Id = Slug($"{browser.Name}-{profile.Directory}"),
                        DisplayName = single ? browser.Name : $"{browser.Name} — {Label(profile)}",
                        ExecutablePath = exe,
                        ProfileDirectory = profile.Directory,
                        AutoDetected = true,
                    });
                }
            }

            foreach (var other in DetectRegisteredBrowsers(chromiumExes))
            {
                targets.Add(other);
            }

            return targets;
        }

        private record ChromiumProfile(string Directory, string Name, string UserName);

        /// <summary>
        /// Reads profile.info_cache from a Chromium "Local State" file. Opened with a
        /// permissive share mode because the browser normally has the file open.
        /// </summary>
        private static List<ChromiumProfile> ReadProfiles(string localStatePath)
        {
            var profiles = new List<ChromiumProfile>();
            if (!File.Exists(localStatePath)) return profiles;

            try
            {
                using var stream = new FileStream(localStatePath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var document = JsonDocument.Parse(stream);

                if (!document.RootElement.TryGetProperty("profile", out var profileNode)) return profiles;
                if (!profileNode.TryGetProperty("info_cache", out var infoCache)) return profiles;

                foreach (var entry in infoCache.EnumerateObject())
                {
                    profiles.Add(new ChromiumProfile(
                        entry.Name,
                        GetStringOrEmpty(entry.Value, "name"),
                        GetStringOrEmpty(entry.Value, "user_name")));
                }
            }
            catch (Exception)
            {
                // A malformed or locked Local State must not stop detection of other browsers.
            }

            // "Default" first, then the rest alphabetically - matches how the browsers list them.
            return profiles
                .OrderBy(p => p.Directory.Equals("Default", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(p => p.Directory, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string GetStringOrEmpty(JsonElement element, string property) =>
            element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;

        /// <summary>
        /// Picks the most human-meaningful label for a profile. The profile's own name wins
        /// when the user has set one; Chromium's untouched defaults ("Person 1", "Profile 2")
        /// are useless, so fall back to the signed-in account then the directory name.
        /// </summary>
        private static string Label(ChromiumProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(profile.Name) && !IsGenericName(profile.Name))
            {
                return profile.Name;
            }

            if (!string.IsNullOrWhiteSpace(profile.UserName))
            {
                var at = profile.UserName.IndexOf('@');
                return at > 0 ? profile.UserName[..at] : profile.UserName;
            }

            return profile.Directory;
        }

        private static bool IsGenericName(string name) =>
            name.StartsWith("Person ", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Default", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Non-Chromium browsers (Firefox, Citrix Enterprise Browser, ...) from the shell's
        /// browser registration. Anything whose exe was already found above is skipped so
        /// profile-aware targets are not duplicated by profile-less ones.
        /// </summary>
        private static List<BrowserTarget> DetectRegisteredBrowsers(HashSet<string> alreadyFound)
        {
            var results = new List<BrowserTarget>();
            var seenExes = new HashSet<string>(alreadyFound, StringComparer.OrdinalIgnoreCase);

            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                using var clients = hive.OpenSubKey(@"SOFTWARE\Clients\StartMenuInternet");
                if (clients is null) continue;

                foreach (var name in clients.GetSubKeyNames())
                {
                    // Never offer ourselves as a routing target.
                    if (name.Equals(RegistrationService.ApplicationKey, StringComparison.OrdinalIgnoreCase)) continue;

                    using var command = clients.OpenSubKey($@"{name}\shell\open\command");
                    var exe = StripQuotes(command?.GetValue(null) as string);
                    if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe)) continue;
                    if (!seenExes.Add(exe)) continue;

                    using var capabilities = clients.OpenSubKey($@"{name}\Capabilities");
                    var friendly = capabilities?.GetValue("ApplicationName") as string;

                    results.Add(new BrowserTarget
                    {
                        Id = Slug(friendly ?? Path.GetFileNameWithoutExtension(exe)),
                        DisplayName = friendly ?? Path.GetFileNameWithoutExtension(exe),
                        ExecutablePath = exe,
                        AutoDetected = true,
                    });
                }
            }

            return results;
        }

        private static string? StripQuotes(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;

            value = value.Trim();
            if (value.StartsWith('"'))
            {
                var end = value.IndexOf('"', 1);
                if (end > 0) return value[1..end];
            }

            // Unquoted commands may carry switches; keep only up to the first ".exe".
            var exeIndex = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            return exeIndex > 0 ? value[..(exeIndex + 4)] : value;
        }

        /// <summary>Stable, filename-safe id, e.g. "Edge-Profile 1" becomes "edge-profile-1".</summary>
        private static string Slug(string value)
        {
            var builder = new StringBuilder(value.Length);
            var lastWasDash = false;

            foreach (var c in value.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                    lastWasDash = false;
                }
                else if (!lastWasDash && builder.Length > 0)
                {
                    builder.Append('-');
                    lastWasDash = true;
                }
            }

            return builder.ToString().Trim('-');
        }

        /// <summary>
        /// Merges freshly detected targets into an existing list, keyed on
        /// (executable, profile directory). Existing entries keep their id, display name and
        /// custom arguments so a re-detect never undoes the user's renames or hand-added rows.
        /// Returns the targets that were newly added.
        /// </summary>
        public static List<BrowserTarget> MergeInto(List<BrowserTarget> existing)
        {
            var added = new List<BrowserTarget>();

            foreach (var candidate in Detect())
            {
                var match = existing.FirstOrDefault(t =>
                    string.Equals(t.ExecutablePath, candidate.ExecutablePath, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(t.ProfileDirectory ?? string.Empty, candidate.ProfileDirectory ?? string.Empty,
                        StringComparison.OrdinalIgnoreCase));

                if (match is not null) continue;

                // Ids must stay unique: rules reference them.
                if (existing.Any(t => t.Id == candidate.Id))
                {
                    candidate.Id = $"{candidate.Id}-{existing.Count + added.Count + 1}";
                }

                existing.Add(candidate);
                added.Add(candidate);
            }

            return added;
        }
    }
}
