using UrlRouter.Services;

namespace UrlRouter.Forms
{
    /// <summary>
    /// The resident agent: a tray icon and a DDE server, and no main window.
    ///
    /// It exists because Outlook is not permitted to start our executable (see
    /// <see cref="DdeServer"/>). With this running, the shell delivers clicked URLs over DDE
    /// to a process that is already alive, and the browser is launched from here instead.
    /// </summary>
    public sealed class AgentContext : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private readonly DdeServer _dde;

        /// <summary>Polls the update feed. Long interval; the cooldown in UpdateService is the real gate.</summary>
        private readonly System.Windows.Forms.Timer _updateTimer;

        private ToolStripMenuItem _updateMenuItem = null!;

        /// <summary>The pending offer, kept so the balloon and the menu item act on the same thing.</summary>
        private UpdateCheckResult? _availableUpdate;

        private bool _updateDialogOpen;

        /// <summary>
        /// True once the agent owns this process. The settings window uses it to tell apart the
        /// two ways it gets opened — from the tray, inside the agent, or standalone via --config —
        /// because only in the first case does installing an update mean ending the process.
        /// </summary>
        public static bool IsRunningInThisProcess { get; private set; }

        // DDE callbacks arrive on the message-pump thread, but routing can show the picker,
        // so work is posted rather than done inline - the callback must return promptly or
        // the shell considers the conversation failed.
        private readonly Control _marshaller;

        public AgentContext()
        {
            IsRunningInThisProcess = true;

            _marshaller = new Control();
            _marshaller.CreateControl();

            _trayIcon = new NotifyIcon
            {
                Icon = LoadIcon(),
                Text = "Url Router",
                Visible = true,
                ContextMenuStrip = BuildMenu(),
            };
            _trayIcon.DoubleClick += (_, _) => OpenSettings();

            _trayIcon.BalloonTipClicked += (_, _) => OfferPendingUpdate();

            _dde = new DdeServer(OnUrlReceived);

            if (!_dde.Start())
            {
                _trayIcon.ShowBalloonTip(5000, "Url Router",
                    "Could not start the DDE listener. Links from Outlook will not open.",
                    ToolTipIcon.Error);
            }

            // The executable left behind by the last update can only be deleted once its image
            // is unloaded, and agent start is the first moment that is guaranteed.
            UpdateService.CleanupRetired();

            // Poll on a timer rather than only at start-up: this process is meant to stay alive
            // for weeks, so a start-up-only check would never fire on a machine that is never
            // signed out. The 20-hour cooldown inside UpdateService is what limits the traffic.
            _updateTimer = new System.Windows.Forms.Timer { Interval = (int)TimeSpan.FromHours(4).TotalMilliseconds };
            _updateTimer.Tick += (_, _) => _ = CheckForUpdatesAsync(userInitiated: false);
            _updateTimer.Start();

            // Give the sign-in storm a moment before reaching for the network.
            var firstCheck = new System.Windows.Forms.Timer { Interval = 90_000 };
            firstCheck.Tick += (_, _) =>
            {
                firstCheck.Stop();
                firstCheck.Dispose();
                _ = CheckForUpdatesAsync(userInitiated: false);
            };
            firstCheck.Start();
        }

        private static Icon LoadIcon()
        {
            try
            {
                var self = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(self))
                {
                    var icon = Icon.ExtractAssociatedIcon(self);
                    if (icon is not null) return icon;
                }
            }
            catch (Exception)
            {
                // Fall through to the stock icon.
            }

            return SystemIcons.Application;
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();

            menu.Items.Add("Settings…", null, (_, _) => OpenSettings());
            menu.Items.Add("Open log folder", null, (_, _) => OpenLogFolder());

            _updateMenuItem = new ToolStripMenuItem("Check for updates…", null,
                (_, _) => _ = CheckForUpdatesAsync(userInitiated: true));
            menu.Items.Add(_updateMenuItem);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit (links will stop working)", null, (_, _) => ExitAgent());

            return menu;
        }

        // ---------------------------------------------------------------- updates

        /// <summary>
        /// Polls the feed and, if there is something to offer, raises a balloon rather than a
        /// dialog. A modal window thrown in front of someone who just clicked a link would be
        /// worse than the problem it reports, so the balloon and the tray menu are the two ways
        /// in and both lead to the same confirmation.
        /// </summary>
        private async Task CheckForUpdatesAsync(bool userInitiated)
        {
            try
            {
                var config = ConfigService.Load();

                if (!userInitiated && !UpdateService.IsCheckDue(config))
                    return;

                // Note what is *not* here: asking from the menu does not switch UpdateCheckEnabled
                // back on. CheckAsync reads only the feed URL, so an explicit check works while
                // background checking stays off - and this method saves the config below, which
                // would otherwise quietly undo a setting the user deliberately turned off.
                var result = await UpdateService.CheckAsync(config).ConfigureAwait(true);

                if (result.Error is null)
                {
                    config.UpdateLastCheckUtc = DateTime.UtcNow;
                    try { ConfigService.Save(config); } catch (Exception) { /* preference only */ }
                }

                if (userInitiated && result.Error is not null)
                {
                    MessageBox.Show($"Could not check for updates:\n\n{result.Error}",
                        "URL Router", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // A user-initiated check ignores a previous "skip": asking is un-skipping.
                var offer = userInitiated ? result.IsNewer : UpdateService.ShouldNotify(result, config);
                if (!offer)
                {
                    if (userInitiated)
                    {
                        MessageBox.Show($"URL Router {result.Current} is up to date.",
                            "URL Router", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return;
                }

                _availableUpdate = result;
                _updateMenuItem.Text = $"Install update {result.Available}…";

                if (userInitiated)
                {
                    OfferPendingUpdate();
                    return;
                }

                RouterLog.Write($"[update] {result.Available} available (running {result.Current})");
                _trayIcon.ShowBalloonTip(10000, "URL Router",
                    $"Version {result.Available} is available. Click to install.",
                    ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                // An update check is never worth taking the agent down for.
                RouterLog.Write($"[update] check threw: {ex.Message}");
            }
        }

        /// <summary>Runs the confirmation for whatever the last check turned up.</summary>
        private void OfferPendingUpdate()
        {
            if (_availableUpdate is null || _updateDialogOpen) return;

            _updateDialogOpen = true;
            _ = OfferAsync();

            async Task OfferAsync()
            {
                try
                {
                    var config = ConfigService.Load();
                    var outcome = await UpdateFlow.OfferAsync(null, _availableUpdate!, config).ConfigureAwait(true);

                    switch (outcome)
                    {
                        case UpdateOutcome.Applied:
                            // The replacement is already starting and is waiting on our mutex,
                            // which is released as this process exits.
                            ExitThread();
                            break;

                        case UpdateOutcome.InstalledPendingRestart:
                            // The new build is in place but nothing was started to take over, so
                            // this process stays alive - it is the only agent there is. Keep
                            // routing on the old image and hand the restart to the user.
                            _availableUpdate = null;
                            _updateMenuItem.Text = "Check for updates…";
                            _trayIcon.ShowBalloonTip(10000, "URL Router",
                                "The update is installed but could not restart automatically. " +
                                "Links keep working; the new version starts at your next sign-in.",
                                ToolTipIcon.Warning);
                            break;

                        case UpdateOutcome.Skipped:
                            _availableUpdate = null;
                            _updateMenuItem.Text = "Check for updates…";
                            break;
                    }
                }
                finally
                {
                    _updateDialogOpen = false;
                }
            }
        }

        /// <summary>Handles a URL delivered over DDE.</summary>
        private void OnUrlReceived(string url)
        {
            try
            {
                _marshaller.BeginInvoke(() =>
                {
                    try
                    {
                        RoutingService.Route(url, "dde");
                    }
                    catch (Exception ex)
                    {
                        RouterLog.Write($"[dde] routing threw: {ex}");
                    }
                });
            }
            catch (Exception ex)
            {
                RouterLog.Write($"[dde] could not marshal URL: {ex.Message}");
            }
        }

        private void OpenSettings()
        {
            // A modeless form keeps the agent responsive to link clicks while it is open.
            var existing = Application.OpenForms.OfType<ConfigForm>().FirstOrDefault();
            if (existing is not null)
            {
                existing.WindowState = FormWindowState.Normal;
                existing.Activate();
                return;
            }

            new ConfigForm().Show();
        }

        private static void OpenLogFolder()
        {
            try
            {
                Directory.CreateDirectory(RouterLog.LogDirectory);
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{RouterLog.LogDirectory}\""));
            }
            catch (Exception ex)
            {
                RouterLog.Write($"could not open log folder: {ex.Message}");
            }
        }

        private void ExitAgent()
        {
            var answer = MessageBox.Show(
                "Stop the URL Router agent?\n\n" +
                "Links clicked in Outlook and Teams will stop opening until it runs again, " +
                "because Windows security policy does not allow Outlook to start it on demand.",
                "URL Router", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (answer != DialogResult.Yes) return;

            RouterLog.Write("agent stopped by user");
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _updateTimer.Dispose();
                _dde.Dispose();
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _marshaller.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
