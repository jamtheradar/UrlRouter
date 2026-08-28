using UrlRouter.Forms;
using UrlRouter.Services;

namespace UrlRouter
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        ///
        ///  Two very different jobs live behind this one exe: the hot path, which Windows
        ///  invokes on every clicked link and which must stay fast, and the management UI.
        ///  WinForms is only initialised on the paths that actually show a window.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            // Windows registers us as: UrlRouter.exe --single-argument <url>
            // The URL is read from the raw command line rather than the parsed args array,
            // because the shell passes it unquoted and a URL containing '&' or a space would
            // otherwise arrive split across several elements.
            var shellUrl = TryGetSingleArgument();
            if (shellUrl is not null)
            {
                return Route(shellUrl);
            }

            // Double-clicked with no arguments: open settings.
            if (args.Length == 0)
            {
                return ShowConfig();
            }

            switch (args[0].TrimStart('-', '/').ToLowerInvariant())
            {
                case "agent":
                    return RunAgent(args);

                case "config":
                case "c":
                    return ShowConfig();

                case "register":
                    return Register();

                case "unregister":
                    return Unregister();

                case "test":
                    if (args.Length < 2)
                    {
                        ConsoleOutput.WriteLine("Usage: UrlRouter --test <url>");
                        ConsoleOutput.Flush("URL Router");
                        return 1;
                    }
                    return Test(args[1]);

                case "check-updates":
                    return CheckUpdates();

                case "version":
                case "v":
                    ConsoleOutput.WriteLine($"URL Router {UpdateService.CurrentVersion}");
                    ConsoleOutput.Flush("URL Router");
                    return 0;

                case "help":
                case "h":
                case "?":
                    return Help();

                default:
                    // Anything that parses as a URL is treated as a link to route, which
                    // covers shells that drop the --single-argument switch.
                    if (Uri.TryCreate(args[0], UriKind.Absolute, out _))
                    {
                        return Route(args[0]);
                    }
                    return Help();
            }
        }

        // ---------------------------------------------------------------- hot path

        private static int Route(string rawUrl)
        {
            // The picker and any error dialog need WinForms; a matched route pays only the
            // initialise call, not a form.
            ApplicationConfiguration.Initialize();
            return RoutingService.Route(rawUrl, "shell") ? 0 : 1;
        }

        /// <summary>
        /// Runs the resident DDE agent. Single-instance: a second copy exits immediately
        /// rather than fighting over the DDE service name.
        /// </summary>
        private static int RunAgent(string[] args)
        {
            // "--agent --wait" is how a just-installed update hands over. The outgoing agent is
            // still alive when it starts its replacement, so without the wait the replacement
            // would be turned away on the spot and the machine would end up with no agent at
            // all - which means dead Outlook links until the next sign-in.
            var isHandover = args.Any(a => a.TrimStart('-', '/').Equals("wait", StringComparison.OrdinalIgnoreCase));

            var mutex = AcquireAgentMutex(isHandover, out var acquired);
            using (mutex)
            {
                if (!acquired)
                {
                    return 0;
                }

                ApplicationConfiguration.Initialize();
                Application.Run(new AgentContext());
                return 0;
            }
        }

        /// <summary>
        /// Claims the single-instance mutex, optionally retrying while an outgoing agent finishes
        /// exiting. Note what "acquired" means here: the flag is <c>createdNew</c>, so it reports
        /// that no other agent holds a handle - the kernel object outlives the owner's exit only
        /// until the last handle closes, which is exactly the moment the successor may start.
        ///
        /// Giving up is a safe failure. Without the mutex this copy simply exits, which is the
        /// same outcome as launching a second agent by hand.
        /// </summary>
        private static Mutex AcquireAgentMutex(bool waitForHandover, out bool acquired)
        {
            var mutex = new Mutex(initiallyOwned: true, RegistrationService.AgentMutexName, out acquired);
            if (acquired || !waitForHandover)
            {
                return mutex;
            }

            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(250);

                mutex.Dispose();
                mutex = new Mutex(initiallyOwned: true, RegistrationService.AgentMutexName, out acquired);
                if (acquired)
                {
                    break;
                }
            }

            return mutex;
        }

        // ---------------------------------------------------------------- management

        private static int ShowConfig()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new ConfigForm());
            return 0;
        }

        private static int Register()
        {
            try
            {
                RegistrationService.Register();

                ConsoleOutput.WriteLine($"Registered \"{RegistrationService.DisplayName}\" at {Environment.ProcessPath}");

                var agentStarted = RegistrationService.EnsureAgentRunning();
                ConsoleOutput.WriteLine(
                    agentStarted
                        ? "Background agent started, and set to start at sign-in."
                        : "WARNING: the background agent could not be started — Outlook links will stay blocked."
                );

                ConsoleOutput.WriteLine();
                ConsoleOutput.WriteLine("Windows does not allow an application to make itself the default browser.");
                ConsoleOutput.WriteLine("Finish in Settings > Apps > Default apps > URL Router,");
                ConsoleOutput.WriteLine("and set it for both HTTP and HTTPS. Opening that page now.");
                ConsoleOutput.Flush("URL Router");

                RegistrationService.OpenWindowsDefaultAppsSettings();
                return 0;
            }
            catch (Exception ex)
            {
                ConsoleOutput.WriteLine($"Registration failed: {ex.Message}");
                ConsoleOutput.Flush("URL Router");
                return 1;
            }
        }

        private static int Unregister()
        {
            try
            {
                RegistrationService.Unregister();
                ConsoleOutput.WriteLine("Unregistered. Pick your normal browser again in Settings > Apps > Default apps.");
                ConsoleOutput.Flush("URL Router");
                return 0;
            }
            catch (Exception ex)
            {
                ConsoleOutput.WriteLine($"Unregister failed: {ex.Message}");
                ConsoleOutput.Flush("URL Router");
                return 1;
            }
        }

        /// <summary>Shows what would happen for a URL without opening anything.</summary>
        private static int Test(string url)
        {
            var config = ConfigService.Load();
            var normalized = UrlNormalizer.Normalize(url, config.UnwrapSafeLinks);

            ConsoleOutput.WriteLine($"Original    : {normalized.Original}");
            ConsoleOutput.WriteLine(
                $"Normalized  : {normalized.Normalized}" + (normalized.WasWrapped ? $"   ({normalized.UnwrapCount} wrapper(s) removed)" : "")
            );

            if (!Uri.TryCreate(normalized.Normalized, UriKind.Absolute, out var uri))
            {
                ConsoleOutput.WriteLine("Result      : not a valid absolute URL - would show an error.");
                ConsoleOutput.Flush("URL Router — Test");
                return 1;
            }

            var match = RuleMatcher.Match(config, uri);

            if (match.Rule is not null)
            {
                ConsoleOutput.WriteLine(
                    $"Matched rule: {match.Rule.HostPattern}"
                        + (string.IsNullOrWhiteSpace(match.Rule.PathPattern) ? "" : $"  path {match.Rule.PathPattern}")
                        + (string.IsNullOrWhiteSpace(match.Rule.Comment) ? "" : $"   ({match.Rule.Comment})")
                );
            }
            else if (match.UsedFallback)
            {
                ConsoleOutput.WriteLine("Matched rule: none - using configured fallback.");
            }
            else
            {
                ConsoleOutput.WriteLine("Matched rule: none - would show the picker.");
            }

            if (match.Target is not null)
            {
                ConsoleOutput.WriteLine($"Target      : {match.Target.DisplayName}");
                ConsoleOutput.WriteLine($"Command     : {BrowserLauncher.DescribeCommand(match.Target, normalized.Normalized)}");
            }

            ConsoleOutput.Flush("URL Router — Test");
            return 0;
        }

        /// <summary>
        /// Polls the update feed and reports what it says, without offering or installing
        /// anything. The counterpart to --test: when someone says updates are not arriving, this
        /// separates "the feed is unreachable" from "there is nothing newer" without guessing.
        /// </summary>
        private static int CheckUpdates()
        {
            var config = ConfigService.Load();
            var result = UpdateService.CheckAsync(config).GetAwaiter().GetResult();

            ConsoleOutput.WriteLine($"Installed : {result.Current}");
            ConsoleOutput.WriteLine($"Feed      : {(string.IsNullOrWhiteSpace(config.UpdateFeedUrl) ? UpdateService.DefaultFeedUrl : config.UpdateFeedUrl)}");

            if (result.Error is not null)
            {
                ConsoleOutput.WriteLine($"Result    : could not check — {result.Error}");
                ConsoleOutput.Flush("URL Router — Updates");
                return 1;
            }

            ConsoleOutput.WriteLine($"Available : {result.Available}");
            ConsoleOutput.WriteLine(
                result.IsNewer
                    ? $"Result    : an update is available{(result.IsRequired ? " and is required" : "")}."
                    : "Result    : up to date.");

            if (result.IsNewer)
            {
                ConsoleOutput.WriteLine($"Download  : {result.Manifest?.Url}");
                ConsoleOutput.WriteLine($"SHA-256   : {result.Manifest?.Sha256}");
            }

            ConsoleOutput.Flush("URL Router — Updates");
            return 0;
        }

        private static int Help()
        {
            ConsoleOutput.WriteLine("URL Router — sends clicked links to the right browser profile.");
            ConsoleOutput.WriteLine();
            ConsoleOutput.WriteLine("  UrlRouter                  Open the settings window");
            ConsoleOutput.WriteLine("  UrlRouter --config         Open the settings window");
            ConsoleOutput.WriteLine("  UrlRouter --agent          Run the resident DDE agent (started automatically at sign-in)");
            ConsoleOutput.WriteLine("  UrlRouter --register       Register as a selectable browser, then open Windows settings");
            ConsoleOutput.WriteLine("  UrlRouter --unregister     Remove the registration");
            ConsoleOutput.WriteLine("  UrlRouter --test <url>     Show which browser a URL would open in, without opening it");
            ConsoleOutput.WriteLine("  UrlRouter <url>            Route a URL now");
            ConsoleOutput.WriteLine("  UrlRouter --check-updates  Poll the update feed and report, without installing");
            ConsoleOutput.WriteLine("  UrlRouter --version        Print the installed version");
            ConsoleOutput.WriteLine();
            ConsoleOutput.WriteLine($"Version: {UpdateService.CurrentVersion}");
            ConsoleOutput.WriteLine($"Config: {ConfigService.ConfigPath}");
            ConsoleOutput.WriteLine($"Log   : {RouterLog.LogPath}");
            ConsoleOutput.Flush("URL Router");
            return 0;
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Extracts the URL from a "--single-argument &lt;url&gt;" command line, reading the raw
        /// string so that nothing is lost to argument splitting. Everything after the switch
        /// is the URL, verbatim - which is precisely what the switch means to Chromium, and
        /// why Edge and Brave register themselves the same way.
        /// </summary>
        private static string? TryGetSingleArgument()
        {
            const string token = "--single-argument";

            var commandLine = Environment.CommandLine;
            if (string.IsNullOrEmpty(commandLine))
                return null;

            // Skip past the executable path so a directory containing the token cannot match.
            var searchFrom = 0;
            if (commandLine[0] == '"')
            {
                var closing = commandLine.IndexOf('"', 1);
                if (closing < 0)
                    return null;
                searchFrom = closing + 1;
            }
            else
            {
                var space = commandLine.IndexOf(' ');
                if (space < 0)
                    return null;
                searchFrom = space;
            }

            var index = commandLine.IndexOf(token, searchFrom, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return null;

            var url = commandLine[(index + token.Length)..].Trim();

            // Some callers quote the URL even with this switch; accept both forms.
            if (url.Length >= 2 && url[0] == '"' && url[^1] == '"')
            {
                url = url[1..^1];
            }

            return string.IsNullOrWhiteSpace(url) ? null : url;
        }
    }
}
