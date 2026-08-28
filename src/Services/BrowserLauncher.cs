using System.Diagnostics;
using System.Text;
using UrlRouter.Models;

namespace UrlRouter.Services
{
    /// <summary>
    /// Launches the chosen browser with the routed URL.
    /// </summary>
    public static class BrowserLauncher
    {
        /// <summary>
        /// Builds the argument list for a target. The URL always goes last, which is what
        /// every Chromium browser expects for "open this address".
        /// </summary>
        public static List<string> BuildArguments(BrowserTarget target, string url)
        {
            var args = new List<string>();

            if (!string.IsNullOrWhiteSpace(target.ProfileDirectory))
            {
                args.Add($"--profile-directory={target.ProfileDirectory}");
            }

            if (!string.IsNullOrWhiteSpace(target.ExtraArguments))
            {
                args.AddRange(SplitArguments(target.ExtraArguments));
            }

            args.Add(url);
            return args;
        }

        /// <summary>Human-readable command line, for the Test tab and the log.</summary>
        public static string DescribeCommand(BrowserTarget target, string url)
        {
            var builder = new StringBuilder();
            builder.Append('"').Append(target.ExecutablePath).Append('"');

            foreach (var arg in BuildArguments(target, url))
            {
                builder.Append(' ');
                builder.Append(arg.Contains(' ') ? $"\"{arg}\"" : arg);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Starts the browser. Returns false (with a reason) rather than throwing, so callers
        /// on the click path can fall back to the picker instead of dying silently.
        /// </summary>
        public static bool TryLaunch(BrowserTarget target, string url, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(target.ExecutablePath) || !File.Exists(target.ExecutablePath))
            {
                error = $"Browser executable not found: {target.ExecutablePath}";
                return false;
            }

            if (IsSelf(target.ExecutablePath))
            {
                // Guard against the one configuration that would hang the machine: pointing a
                // target back at this app, which Windows would then hand the URL to again.
                error = "Target points at UrlRouter itself, which would loop forever.";
                return false;
            }

            try
            {
                var info = new ProcessStartInfo(target.ExecutablePath)
                {
                    // Must stay false. UseShellExecute would resolve the URL through the shell,
                    // which is registered to this very app - an infinite launch loop.
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(target.ExecutablePath) ?? string.Empty,
                };

                foreach (var arg in BuildArguments(target, url))
                {
                    info.ArgumentList.Add(arg);
                }

                Process.Start(info);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool IsSelf(string executablePath)
        {
            try
            {
                var self = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(self) &&
                    string.Equals(Path.GetFullPath(executablePath), Path.GetFullPath(self),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Also catch a *different copy* of this tool - e.g. the published build while
                // running the debug one. Both would be registered to handle http(s).
                var registered = RegistrationService.GetRegisteredExecutablePath();
                return !string.IsNullOrEmpty(registered) &&
                       string.Equals(Path.GetFullPath(executablePath), Path.GetFullPath(registered),
                           StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Splits a user-entered switch string on spaces, honouring double quotes so
        /// arguments such as -P "Work Profile" survive intact.
        /// </summary>
        public static IEnumerable<string> SplitArguments(string value)
        {
            var current = new StringBuilder();
            var inQuotes = false;

            foreach (var c in value)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        yield return current.ToString();
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0) yield return current.ToString();
        }
    }
}
