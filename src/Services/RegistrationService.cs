using Microsoft.Win32;
using System.Security.AccessControl;
using System.Security.Principal;

namespace UrlRouter.Services
{
    /// <summary>Current state of the Windows shell registration.</summary>
    /// <param name="IsRegistered">Whether this exe is registered as a candidate browser.</param>
    /// <param name="RegisteredExecutablePath">Path Windows will launch, or null.</param>
    /// <param name="PathIsStale">Registered, but pointing at a different copy of this exe.</param>
    /// <param name="HttpProgId">Current user default for http, e.g. "BraveHTML".</param>
    /// <param name="HttpsProgId">Current user default for https.</param>
    /// <param name="AppContainerAccessGranted">
    /// Whether packaged apps (new Outlook, Teams) are allowed to launch our executable.
    /// </param>
    /// <param name="AgentRunning">
    /// Whether the resident DDE agent is alive. Without it, Outlook clicks are blocked by
    /// Attack Surface Reduction, so this is not optional on a managed device.
    /// </param>
    public record RegistrationStatus(
        bool IsRegistered,
        string? RegisteredExecutablePath,
        bool PathIsStale,
        string? HttpProgId,
        string? HttpsProgId,
        bool AppContainerAccessGranted,
        bool AgentRunning)
    {
        /// <summary>True only when Windows is actually sending links here.</summary>
        public bool IsDefaultBrowser =>
            RegistrationService.ProgId.Equals(HttpProgId, StringComparison.OrdinalIgnoreCase) &&
            RegistrationService.ProgId.Equals(HttpsProgId, StringComparison.OrdinalIgnoreCase);

        public string Summary
        {
            get
            {
                if (!IsRegistered) return "Not registered with Windows.";
                if (PathIsStale) return "Registered, but pointing at a different copy of UrlRouter. Re-register.";

                if (!AppContainerAccessGranted)
                {
                    return "Registered, but packaged apps cannot launch it — links from the new Outlook " +
                           "and Teams will fail with \"Windows cannot access the specified device, path, or file\". " +
                           "Click Register again to fix the permissions.";
                }

                if (!AgentRunning)
                {
                    return "Registered, but the background agent is not running — links clicked in " +
                           "Outlook and Teams will be blocked by Attack Surface Reduction. Start it below.";
                }

                if (IsDefaultBrowser) return "Active — Windows is sending http and https links here.";

                var current = HttpsProgId ?? HttpProgId ?? "another browser";
                return $"Registered, but Windows still opens links with {current}. " +
                       "Choose \"URL Router\" in Settings to activate.";
            }
        }
    }

    /// <summary>
    /// Registers this application with Windows as a selectable browser.
    ///
    /// Everything is written under HKEY_CURRENT_USER, which needs no elevation - the same
    /// per-user scheme Firefox uses when installed without admin rights.
    ///
    /// Note the deliberate limitation: since Windows 10, an application cannot make itself
    /// the default browser. The UserChoice value is protected by a per-user hash the shell
    /// verifies, so all this class can do is make the app *choosable*. The final selection
    /// happens in Settings, and <see cref="GetStatus"/> reports whether it has been made.
    /// </summary>
    public static class RegistrationService
    {
        /// <summary>Key name under Clients\StartMenuInternet and RegisteredApplications.</summary>
        public const string ApplicationKey = "UrlRouter";

        /// <summary>ProgId that http/https are associated with.</summary>
        public const string ProgId = "UrlRouterURL";

        public const string DisplayName = "URL Router";

        private const string Description =
            "Routes links from Outlook and Teams to the right browser profile.";

        private const string ClassesPath = @"SOFTWARE\Classes\" + ProgId;
        private const string ClientPath = @"SOFTWARE\Clients\StartMenuInternet\" + ApplicationKey;
        private const string CapabilitiesPath = ClientPath + @"\Capabilities";
        private const string RegisteredApplicationsPath = @"SOFTWARE\RegisteredApplications";

        private static string ExecutablePath =>
            Environment.ProcessPath ?? throw new InvalidOperationException("Cannot resolve own executable path.");

        public static void Register()
        {
            var exe = ExecutablePath;
            var icon = $"\"{exe}\",0";

            // Must happen before anything else is worth doing - see the method's remarks.
            GrantAppContainerAccess(exe);

            // --- ProgId: what actually opens a URL -------------------------------------
            // "--single-argument %1" (unquoted %1) is copied verbatim from how Edge and Brave
            // register themselves. It is the only form that survives URLs containing spaces,
            // '&' or '%' without the shell mangling them into multiple arguments.
            using (var progId = Registry.CurrentUser.CreateSubKey(ClassesPath))
            {
                progId.SetValue(null, DisplayName + " URL");
                progId.SetValue("FriendlyTypeName", DisplayName + " URL");

                using (var defaultIcon = progId.CreateSubKey("DefaultIcon"))
                {
                    defaultIcon.SetValue(null, icon);
                }

                using (var command = progId.CreateSubKey(@"shell\open\command"))
                {
                    command.SetValue(null, $"\"{exe}\" --single-argument %1");
                }

                // The DDE path is what actually carries links from Outlook. When the agent is
                // running the shell delivers the URL over a DDE conversation and never starts
                // a process, so the ASR rule blocking Outlook from creating child processes
                // has nothing to block. The `command` above is only the fallback for callers
                // that are allowed to launch us.
                using (var ddeexec = progId.CreateSubKey(@"shell\open\ddeexec"))
                {
                    ddeexec.SetValue(null, "[open(\"%1\")]");

                    using (var application = ddeexec.CreateSubKey("Application"))
                    {
                        application.SetValue(null, DdeServer.ServiceName);
                    }

                    using (var topic = ddeexec.CreateSubKey("Topic"))
                    {
                        topic.SetValue(null, DdeServer.TopicName);
                    }
                }
            }

            // --- Browser registration: what makes it appear in Default Apps -------------
            using (var client = Registry.CurrentUser.CreateSubKey(ClientPath))
            {
                client.SetValue(null, DisplayName);

                using (var defaultIcon = client.CreateSubKey("DefaultIcon"))
                {
                    defaultIcon.SetValue(null, icon);
                }

                // Launching from the Start menu should open the settings UI, not a blank browser.
                using (var command = client.CreateSubKey(@"shell\open\command"))
                {
                    command.SetValue(null, $"\"{exe}\" --config");
                }

                // Some Windows builds skip applications without InstallInfo when populating
                // the Default Apps list.
                using (var installInfo = client.CreateSubKey("InstallInfo"))
                {
                    installInfo.SetValue("IconsVisible", 1, RegistryValueKind.DWord);
                    installInfo.SetValue("ReinstallCommand", $"\"{exe}\" --register");
                    installInfo.SetValue("ShowIconsCommand", $"\"{exe}\" --register");
                    installInfo.SetValue("HideIconsCommand", string.Empty);
                }
            }

            using (var capabilities = Registry.CurrentUser.CreateSubKey(CapabilitiesPath))
            {
                capabilities.SetValue("ApplicationName", DisplayName);
                capabilities.SetValue("ApplicationDescription", Description);
                capabilities.SetValue("ApplicationIcon", icon);

                using (var urlAssociations = capabilities.CreateSubKey("URLAssociations"))
                {
                    urlAssociations.SetValue("http", ProgId);
                    urlAssociations.SetValue("https", ProgId);
                }

                using (var startMenu = capabilities.CreateSubKey("StartMenu"))
                {
                    startMenu.SetValue("StartMenuInternet", ApplicationKey);
                }
            }

            using (var registered = Registry.CurrentUser.CreateSubKey(RegisteredApplicationsPath))
            {
                registered.SetValue(ApplicationKey, CapabilitiesPath);
            }

            // The DDE handoff only works while the agent is alive, so it has to come back
            // after every sign-in or links silently start failing again.
            using (var run = Registry.CurrentUser.CreateSubKey(RunPath))
            {
                run.SetValue(ApplicationKey, $"\"{exe}\" --agent");
            }

            RouterLog.Write($"registered handler at {exe}");
        }

        private const string RunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>Named mutex the agent holds; the cheapest way to ask "is it running?".</summary>
        public const string AgentMutexName = @"Local\UrlRouterAgent";

        public static bool IsAgentRunning()
        {
            try
            {
                // Opening succeeds only if the agent created it.
                using var existing = System.Threading.Mutex.OpenExisting(AgentMutexName);
                return true;
            }
            catch (System.Threading.WaitHandleCannotBeOpenedException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Starts the resident agent if it is not already running.</summary>
        public static bool EnsureAgentRunning()
        {
            if (IsAgentRunning()) return true;

            try
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe)) return false;

                // UseShellExecute so the long-lived agent does not inherit our standard
                // handles: it outlives us by design, and a script that redirects our output
                // should not be left holding a pipe the agent never closes.
                // Safe here because this is a direct executable path, not a URL, so it cannot
                // re-enter our own protocol handler.
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe)
                {
                    UseShellExecute = true,
                    Arguments = "--agent",
                });

                // Give it a moment to claim the mutex so callers can report honestly.
                for (var i = 0; i < 20 && !IsAgentRunning(); i++)
                {
                    System.Threading.Thread.Sleep(100);
                }

                return IsAgentRunning();
            }
            catch (Exception ex)
            {
                RouterLog.Write($"could not start agent: {ex.Message}");
                return false;
            }
        }

        public static void Unregister()
        {
            using (var registered = Registry.CurrentUser.OpenSubKey(RegisteredApplicationsPath, writable: true))
            {
                registered?.DeleteValue(ApplicationKey, throwOnMissingValue: false);
            }

            using (var run = Registry.CurrentUser.OpenSubKey(RunPath, writable: true))
            {
                run?.DeleteValue(ApplicationKey, throwOnMissingValue: false);
            }

            DeleteTree(@"SOFTWARE\Clients\StartMenuInternet", ApplicationKey);
            DeleteTree(@"SOFTWARE\Classes", ProgId);

            RouterLog.Write("unregistered handler");
        }

        private static void DeleteTree(string parentPath, string subKey)
        {
            using var parent = Registry.CurrentUser.OpenSubKey(parentPath, writable: true);
            if (parent is null) return;

            try
            {
                parent.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false);
            }
            catch (Exception ex)
            {
                RouterLog.Write($"could not remove {parentPath}\\{subKey}: {ex.Message}");
            }
        }

        /// <summary>
        /// Well-known SID for ALL APPLICATION PACKAGES. Used literally rather than by name
        /// because the account name is localised.
        /// </summary>
        private const string AllApplicationPackagesSid = "S-1-15-2-1";

        /// <summary>Well-known SID for ALL RESTRICTED APPLICATION PACKAGES.</summary>
        private const string AllRestrictedApplicationPackagesSid = "S-1-15-2-2";

        /// <summary>
        /// Lets packaged (AppContainer) applications launch our executable.
        ///
        /// The new Outlook and Teams are MSIX packages running in an AppContainer, and an
        /// AppContainer process can only touch files whose ACL explicitly grants one of the
        /// application-package SIDs. Everything under C:\Program Files carries those ACEs by
        /// default - which is why Brave and Edge work as handlers - but a folder we create
        /// under %LOCALAPPDATA% does not.
        ///
        /// Without this, Windows refuses to start the handler and the user sees
        /// "Windows cannot access the specified device, path, or file" with the URL as the
        /// dialog title, and nothing is ever logged because our process never runs.
        ///
        /// The ACEs go on the containing directory with inheritance, so a re-publish that
        /// replaces the exe stays working.
        /// </summary>
        public static void GrantAppContainerAccess(string executablePath)
        {
            var directory = Path.GetDirectoryName(executablePath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;

            try
            {
                var info = new DirectoryInfo(directory);
                var security = info.GetAccessControl();

                foreach (var sid in new[] { AllApplicationPackagesSid, AllRestrictedApplicationPackagesSid })
                {
                    security.AddAccessRule(new FileSystemAccessRule(
                        new SecurityIdentifier(sid),
                        FileSystemRights.ReadAndExecute,
                        InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow));
                }

                info.SetAccessControl(security);
                RouterLog.Write($"granted app-package read/execute on {directory}");
            }
            catch (Exception ex)
            {
                // Not fatal: links from ordinary Win32 apps still work. Only packaged apps
                // (new Outlook, Teams) are affected, and GetStatus surfaces that.
                RouterLog.Write($"could not grant app-package access on {directory}: {ex.Message}");
            }
        }

        /// <summary>
        /// True when packaged apps can execute the given file. Checked on the file itself
        /// rather than the directory, since that is what the shell actually launches.
        /// </summary>
        public static bool HasAppContainerAccess(string? executablePath)
        {
            if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath)) return false;

            try
            {
                var rules = new FileInfo(executablePath)
                    .GetAccessControl()
                    .GetAccessRules(true, true, typeof(SecurityIdentifier));

                foreach (FileSystemAccessRule rule in rules)
                {
                    if (rule.AccessControlType != AccessControlType.Allow) continue;
                    if (!rule.FileSystemRights.HasFlag(FileSystemRights.ReadAndExecute)) continue;

                    var sid = rule.IdentityReference.Value;
                    if (sid == AllApplicationPackagesSid || sid == AllRestrictedApplicationPackagesSid)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception)
            {
                // Unknown rather than known-bad; do not nag the user over an unreadable ACL.
                return true;
            }
        }

        public static RegistrationStatus GetStatus()
        {
            var registeredExe = GetRegisteredExecutablePath();
            var isRegistered = !string.IsNullOrEmpty(registeredExe);

            var stale = false;
            if (isRegistered)
            {
                try
                {
                    var self = Environment.ProcessPath;
                    stale = !string.IsNullOrEmpty(self) &&
                            !string.Equals(Path.GetFullPath(registeredExe!), Path.GetFullPath(self),
                                StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception)
                {
                    stale = false;
                }
            }

            return new RegistrationStatus(
                isRegistered,
                registeredExe,
                stale,
                GetUserChoiceProgId("http"),
                GetUserChoiceProgId("https"),
                !isRegistered || HasAppContainerAccess(registeredExe),
                IsAgentRunning());
        }

        /// <summary>Executable path currently baked into our shell registration, if any.</summary>
        public static string? GetRegisteredExecutablePath()
        {
            using var command = Registry.CurrentUser.OpenSubKey(ClassesPath + @"\shell\open\command");
            var value = command?.GetValue(null) as string;
            if (string.IsNullOrWhiteSpace(value)) return null;

            // Stored as: "C:\path\UrlRouter.exe" --single-argument %1
            if (!value.StartsWith('"')) return null;

            var end = value.IndexOf('"', 1);
            return end > 1 ? value[1..end] : null;
        }

        /// <summary>
        /// The protocol handler Windows is actually using. Read-only by necessity - this
        /// value is hash-protected and can only be changed by the user via Settings.
        /// </summary>
        public static string? GetUserChoiceProgId(string protocol)
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                $@"SOFTWARE\Microsoft\Windows\Shell\Associations\UrlAssociations\{protocol}\UserChoice");

            return key?.GetValue("ProgId") as string;
        }

        /// <summary>Opens the Windows page where the user makes this the default browser.</summary>
        public static void OpenWindowsDefaultAppsSettings()
        {
            try
            {
                // ms-settings: is a shell protocol, not an http URL, so it cannot loop back here.
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    "ms-settings:defaultapps")
                {
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                RouterLog.Write($"could not open default apps settings: {ex.Message}");
            }
        }
    }
}
