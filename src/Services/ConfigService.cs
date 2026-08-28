using System.Text.Json;
using UrlRouter.Models;

namespace UrlRouter.Services
{
    /// <summary>
    /// Loads and saves %APPDATA%\UrlRouter\config.json, seeding a working
    /// configuration from the browsers actually installed on first run.
    /// </summary>
    public static class ConfigService
    {
        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

        public static string ConfigDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UrlRouter");

        public static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

        public static RouterConfig Load()
        {
            if (!File.Exists(ConfigPath))
            {
                var seeded = CreateDefault();
                Save(seeded);
                return seeded;
            }

            try
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<RouterConfig>(json, SerializerOptions) ?? CreateDefault();
            }
            catch (Exception ex)
            {
                // A corrupt config must not silently swallow links. Keep the bad file for
                // inspection, fall back to defaults, and say so in the log.
                RouterLog.Write($"config load failed ({ex.Message}); using defaults");
                TryBackupCorruptConfig();
                return CreateDefault();
            }
        }

        public static void Save(RouterConfig config)
        {
            Directory.CreateDirectory(ConfigDirectory);

            // Write via a temp file so an interrupted save cannot truncate a working config.
            var temp = ConfigPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(config, SerializerOptions));
            File.Move(temp, ConfigPath, overwrite: true);
        }

        private static void TryBackupCorruptConfig()
        {
            try
            {
                File.Copy(ConfigPath, ConfigPath + ".bad", overwrite: true);
            }
            catch (Exception)
            {
                // Best effort only.
            }
        }

        /// <summary>
        /// Builds a first-run config: every detected browser profile as a target.
        /// Fallback is deliberately left null so unmatched URLs raise the picker rather than
        /// being guessed at.
        /// </summary>
        public static RouterConfig CreateDefault()
        {
            var config = new RouterConfig { Targets = BrowserDetectionService.Detect(), FallbackTargetId = null };

            return config;
        }

        /// <summary>
        /// Finds the target for a browser, optionally preferring one whose label mentions
        /// <paramref name="profileHint"/>
        /// </summary>
        private static BrowserTarget? FindTarget(RouterConfig config, string browser, string? profileHint)
        {
            var candidates = config.Targets.Where(t => t.DisplayName.StartsWith(browser, StringComparison.OrdinalIgnoreCase)).ToList();

            if (candidates.Count == 0)
                return null;

            if (profileHint is not null)
            {
                var hinted = candidates.FirstOrDefault(t => t.DisplayName.Contains(profileHint, StringComparison.OrdinalIgnoreCase));
                if (hinted is not null)
                    return hinted;
            }

            return candidates[0];
        }
    }
}
