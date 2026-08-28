using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using UrlRouter.Models;
using UrlRouter.Services;

namespace UrlRouter.Forms
{
    /// <summary>
    /// Settings window: rules, browser targets, a dry-run tester, and the Windows
    /// registration status.
    /// </summary>
    public partial class ConfigForm : Form
    {
        private const string AskSentinel = "";
        private const string BrowseColumnName = "BrowseColumn";

        private readonly RouterConfig _config;
        private readonly BindingList<RoutingRule> _rules;
        private readonly BindingList<BrowserTarget> _targets;

        private bool _dirty;
        private bool _loading = true;

        /// <summary>Result of the last check on this tab, kept so Install acts on what is shown.</summary>
        private UpdateCheckResult? _availableUpdate;

        public ConfigForm()
        {
            InitializeComponent();

            _config = ConfigService.Load();

            // BindingList wraps the very same List instances the config holds, so grid edits
            // land straight on the object that gets serialised.
            _rules = new BindingList<RoutingRule>(_config.Rules);
            _targets = new BindingList<BrowserTarget>(_config.Targets);

            BuildRulesGrid();
            BuildTargetsGrid();
            WireEvents();

            LoadSettingsIntoUi();
            RefreshStatus();
            RefreshLog();

            ConfigPathLabel.Text = ConfigService.ConfigPath;
            _loading = false;
        }

        // ------------------------------------------------------------------ grids

        private void BuildRulesGrid()
        {
            RulesGrid.AutoGenerateColumns = false;
            RulesGrid.Columns.Clear();

            RulesGrid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "Enabled",
                HeaderText = "On",
                DataPropertyName = nameof(RoutingRule.Enabled),
                Width = 44,
            });

            RulesGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "HostPattern",
                HeaderText = "Host pattern",
                DataPropertyName = nameof(RoutingRule.HostPattern),
                Width = 230,
            });

            RulesGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "PathPattern",
                HeaderText = "Path pattern (optional)",
                DataPropertyName = nameof(RoutingRule.PathPattern),
                Width = 160,
            });

            RulesGrid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "TargetId",
                HeaderText = "Open in",
                DataPropertyName = nameof(RoutingRule.TargetId),
                DataSource = _targets,
                DisplayMember = nameof(BrowserTarget.DisplayName),
                ValueMember = nameof(BrowserTarget.Id),
                Width = 180,
            });

            RulesGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Comment",
                HeaderText = "Note",
                DataPropertyName = nameof(RoutingRule.Comment),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            });

            RulesGrid.DataSource = _rules;
        }

        private void BuildTargetsGrid()
        {
            TargetsGrid.AutoGenerateColumns = false;
            TargetsGrid.Columns.Clear();

            TargetsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "DisplayName",
                HeaderText = "Name",
                DataPropertyName = nameof(BrowserTarget.DisplayName),
                Width = 190,
            });

            TargetsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ExecutablePath",
                HeaderText = "Executable",
                DataPropertyName = nameof(BrowserTarget.ExecutablePath),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            });

            TargetsGrid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = BrowseColumnName,
                HeaderText = "",
                Text = "…",
                UseColumnTextForButtonValue = true,
                Width = 30,
            });

            TargetsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ProfileDirectory",
                HeaderText = "Profile",
                DataPropertyName = nameof(BrowserTarget.ProfileDirectory),
                Width = 110,
            });

            TargetsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ExtraArguments",
                HeaderText = "Extra switches",
                DataPropertyName = nameof(BrowserTarget.ExtraArguments),
                Width = 150,
            });

            TargetsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Id",
                HeaderText = "Id (used by rules)",
                DataPropertyName = nameof(BrowserTarget.Id),
                Width = 140,
                ReadOnly = true,
                DefaultCellStyle = { ForeColor = SystemColors.GrayText },
            });

            TargetsGrid.DataSource = _targets;
        }

        private void WireEvents()
        {
            AddRuleButton.Click += (_, _) => AddRule();
            DeleteRuleButton.Click += (_, _) => DeleteRule();
            MoveUpButton.Click += (_, _) => MoveRule(-1);
            MoveDownButton.Click += (_, _) => MoveRule(1);

            AddTargetButton.Click += (_, _) => AddTarget();
            DeleteTargetButton.Click += (_, _) => DeleteTarget();
            RedetectButton.Click += (_, _) => Redetect();
            TargetsGrid.CellContentClick += TargetsGrid_CellContentClick;
            TargetsGrid.CellValueChanged += (_, _) => RulesGrid.Invalidate();

            TestButton.Click += (_, _) => RunTest(launch: false);
            TestLaunchButton.Click += (_, _) => RunTest(launch: true);
            TestUrlTextBox.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                RunTest(launch: false);
            };

            RegisterButton.Click += (_, _) => DoRegister();
            UnregisterButton.Click += (_, _) => DoUnregister();
            DefaultAppsButton.Click += (_, _) => RegistrationService.OpenWindowsDefaultAppsSettings();
            RefreshStatusButton.Click += (_, _) => RefreshStatus();
            StartAgentButton.Click += (_, _) =>
            {
                if (!RegistrationService.EnsureAgentRunning())
                {
                    MessageBox.Show("The background agent could not be started. See the log for details.",
                        "URL Router", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                RefreshStatus();
            };
            RefreshLogButton.Click += (_, _) => RefreshLog();
            OpenLogFolderButton.Click += (_, _) => OpenConfigFolder();

            CheckForUpdatesButton.Click += (_, _) => _ = CheckForUpdatesAsync();
            InstallUpdateButton.Click += (_, _) => _ = InstallUpdateAsync();
            ReleasePageButton.Click += (_, _) => UpdateFlow.OpenReleasePage(_availableUpdate?.Manifest);
            UpdateCheckEnabledCheckBox.CheckedChanged += (_, _) => MarkDirty();
            UpdateFeedTextBox.TextChanged += (_, _) => MarkDirty();

            SaveButton.Click += (_, _) => SaveConfig();
            CloseFormButton.Click += (_, _) => Close();

            // A rule pointing at a deleted target would otherwise raise a modal data error
            // every time the grid paints.
            RulesGrid.DataError += (_, e) => e.ThrowException = false;
            TargetsGrid.DataError += (_, e) => e.ThrowException = false;

            _rules.ListChanged += (_, _) => MarkDirty();
            _targets.ListChanged += (_, _) => MarkDirty();
            FallbackCombo.SelectedIndexChanged += (_, _) => MarkDirty();
            LogEnabledCheckBox.CheckedChanged += (_, _) => MarkDirty();
            UnwrapCheckBox.CheckedChanged += (_, _) => MarkDirty();

            FormClosing += ConfigForm_FormClosing;
        }

        private void MarkDirty()
        {
            if (_loading) return;

            _dirty = true;
            Text = "URL Router *";
        }

        // ------------------------------------------------------------------ rules tab

        private void AddRule()
        {
            _rules.Add(new RoutingRule
            {
                Enabled = true,
                HostPattern = "example.com",
                TargetId = _targets.FirstOrDefault()?.Id ?? string.Empty,
            });

            RulesGrid.CurrentCell = RulesGrid.Rows[_rules.Count - 1].Cells["HostPattern"];
            RulesGrid.BeginEdit(true);
        }

        private void DeleteRule()
        {
            if (RulesGrid.CurrentRow?.DataBoundItem is not RoutingRule rule) return;
            _rules.Remove(rule);
        }

        /// <summary>Reorders a rule. Order is meaningful: the first match wins.</summary>
        private void MoveRule(int offset)
        {
            if (RulesGrid.CurrentRow?.DataBoundItem is not RoutingRule rule) return;

            var index = _rules.IndexOf(rule);
            var target = index + offset;
            if (target < 0 || target >= _rules.Count) return;

            _rules.RemoveAt(index);
            _rules.Insert(target, rule);

            RulesGrid.ClearSelection();
            RulesGrid.Rows[target].Selected = true;
            RulesGrid.CurrentCell = RulesGrid.Rows[target].Cells["HostPattern"];
        }

        // ------------------------------------------------------------------ browsers tab

        private void AddTarget()
        {
            var path = PromptForExecutable(null);
            if (path is null) return;

            var name = Path.GetFileNameWithoutExtension(path);
            _targets.Add(new BrowserTarget
            {
                Id = MakeUniqueId(name),
                DisplayName = name,
                ExecutablePath = path,
            });

            TargetsGrid.CurrentCell = TargetsGrid.Rows[_targets.Count - 1].Cells["DisplayName"];
            TargetsGrid.BeginEdit(true);
        }

        private void DeleteTarget()
        {
            if (TargetsGrid.CurrentRow?.DataBoundItem is not BrowserTarget target) return;

            var dependents = _rules.Where(r => r.TargetId == target.Id).ToList();
            if (dependents.Count > 0)
            {
                var answer = MessageBox.Show(
                    $"{dependents.Count} rule(s) point at \"{target.DisplayName}\".\n\n" +
                    "Delete it anyway? Those rules will be left without a browser and will be skipped.",
                    "URL Router", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (answer != DialogResult.Yes) return;
            }

            _targets.Remove(target);

            if (_config.FallbackTargetId == target.Id)
            {
                _config.FallbackTargetId = null;
            }

            PopulateFallbackCombo();
        }

        private void Redetect()
        {
            var added = BrowserDetectionService.MergeInto(_config.Targets);

            // The bound list needs telling that the underlying List changed behind its back.
            _targets.ResetBindings();
            PopulateFallbackCombo();

            if (added.Count == 0)
            {
                MessageBox.Show("No new browser profiles were found.",
                    "URL Router", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MarkDirty();
            MessageBox.Show(
                "Added:\n\n  " + string.Join("\n  ", added.Select(t => t.DisplayName)),
                "URL Router", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void TargetsGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (TargetsGrid.Columns[e.ColumnIndex].Name != BrowseColumnName) return;
            if (TargetsGrid.Rows[e.RowIndex].DataBoundItem is not BrowserTarget target) return;

            var path = PromptForExecutable(target.ExecutablePath);
            if (path is null) return;

            target.ExecutablePath = path;
            _targets.ResetItem(e.RowIndex);
            MarkDirty();
        }

        private static string? PromptForExecutable(string? current)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select a browser executable",
                Filter = "Programs (*.exe)|*.exe|All files (*.*)|*.*",
                CheckFileExists = true,
            };

            if (!string.IsNullOrWhiteSpace(current) && File.Exists(current))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(current);
                dialog.FileName = Path.GetFileName(current);
            }

            return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
        }

        private string MakeUniqueId(string seed)
        {
            var baseId = new string(seed.ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');

            if (string.IsNullOrEmpty(baseId)) baseId = "browser";
            if (_targets.All(t => t.Id != baseId)) return baseId;

            var suffix = 2;
            while (_targets.Any(t => t.Id == $"{baseId}-{suffix}")) suffix++;
            return $"{baseId}-{suffix}";
        }

        // ------------------------------------------------------------------ test tab

        private void RunTest(bool launch)
        {
            RulesGrid.EndEdit();
            TargetsGrid.EndEdit();

            var input = TestUrlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                TestResultTextBox.Text = "Enter a URL first.";
                return;
            }

            var report = new StringBuilder();
            var normalized = UrlNormalizer.Normalize(input, UnwrapCheckBox.Checked);

            report.AppendLine($"Original    : {normalized.Original}");
            report.AppendLine($"Normalized  : {normalized.Normalized}");
            if (normalized.WasWrapped)
            {
                report.AppendLine($"              ({normalized.UnwrapCount} Safe Links wrapper(s) removed)");
            }
            report.AppendLine();

            if (!Uri.TryCreate(normalized.Normalized, UriKind.Absolute, out var uri))
            {
                report.AppendLine("Not a valid absolute URL — routing would fail.");
                TestResultTextBox.Text = report.ToString();
                return;
            }

            report.AppendLine($"Host        : {uri.Host}");
            report.AppendLine($"Path        : {uri.AbsolutePath}");
            report.AppendLine();

            var match = RuleMatcher.Match(_config, uri);

            if (match.Rule is not null)
            {
                report.AppendLine($"Matched rule: {match.Rule.HostPattern}"
                                  + (string.IsNullOrWhiteSpace(match.Rule.PathPattern)
                                      ? string.Empty
                                      : $"   path {match.Rule.PathPattern}"));
                if (!string.IsNullOrWhiteSpace(match.Rule.Comment))
                {
                    report.AppendLine($"              {match.Rule.Comment}");
                }
            }
            else if (match.UsedFallback)
            {
                report.AppendLine("Matched rule: none — using the configured fallback.");
            }
            else
            {
                report.AppendLine("Matched rule: none — the picker would be shown.");
            }

            if (match.Target is not null)
            {
                report.AppendLine($"Target      : {match.Target.DisplayName}");
                report.AppendLine();
                report.AppendLine("Command:");
                report.AppendLine("  " + BrowserLauncher.DescribeCommand(match.Target, normalized.Normalized));
            }

            TestResultTextBox.Text = report.ToString();

            if (!launch) return;

            var target = match.Target ?? PickerForm.Choose(_config, uri, normalized.Normalized);
            if (target is null) return;

            if (!BrowserLauncher.TryLaunch(target, normalized.Normalized, out var error))
            {
                MessageBox.Show(error, "URL Router", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ------------------------------------------------------------------ setup tab

        private void LoadSettingsIntoUi()
        {
            LogEnabledCheckBox.Checked = _config.LogEnabled;
            UnwrapCheckBox.Checked = _config.UnwrapSafeLinks;

            UpdateCheckEnabledCheckBox.Checked = _config.UpdateCheckEnabled;
            UpdateFeedTextBox.Text = string.IsNullOrWhiteSpace(_config.UpdateFeedUrl)
                ? RouterConfig.DefaultUpdateFeedUrl
                : _config.UpdateFeedUrl;

            VersionLabel.Text = $"URL Router {UpdateService.CurrentVersion}   —   {Environment.ProcessPath}";
            UpdateStatusLabel.Text = _config.UpdateLastCheckUtc == DateTime.MinValue
                ? "Not checked yet."
                : $"Last checked {_config.UpdateLastCheckUtc.ToLocalTime():g}.";

            if (!string.IsNullOrWhiteSpace(_config.UpdateSkippedVersion))
            {
                UpdateStatusLabel.Text += $" Version {_config.UpdateSkippedVersion} was skipped.";
            }

            PopulateFallbackCombo();
        }

        private record FallbackChoice(string Value, string Label);

        private void PopulateFallbackCombo()
        {
            var wasLoading = _loading;
            _loading = true;

            var choices = new List<FallbackChoice>
            {
                new(AskSentinel, "Ask me — show the picker"),
            };
            choices.AddRange(_targets.Select(t => new FallbackChoice(t.Id, t.DisplayName)));

            FallbackCombo.DataSource = choices;
            FallbackCombo.DisplayMember = nameof(FallbackChoice.Label);
            FallbackCombo.ValueMember = nameof(FallbackChoice.Value);
            FallbackCombo.SelectedValue = _config.FallbackTargetId ?? AskSentinel;

            _loading = wasLoading;
        }

        private void RefreshStatus()
        {
            var status = RegistrationService.GetStatus();

            var text = new StringBuilder(status.Summary);
            if (status.IsRegistered)
            {
                text.AppendLine();
                text.Append($"Registered executable: {status.RegisteredExecutablePath}");
                text.AppendLine();
                text.Append(status.AgentRunning
                    ? "Background agent: running (this is what receives links from Outlook)."
                    : "Background agent: NOT running.");
            }
            if (!status.IsDefaultBrowser)
            {
                text.AppendLine();
                text.Append("Windows does not let an app set itself as default — the choice has to be made in Settings.");
            }

            StatusLabel.Text = text.ToString();
            StatusLabel.ForeColor = status.IsDefaultBrowser
                ? Color.FromArgb(0, 110, 40)
                : status.PathIsStale ? Color.FromArgb(170, 60, 0) : SystemColors.ControlText;

            UnregisterButton.Enabled = status.IsRegistered;
            RegisterButton.Text = status.IsRegistered && !status.PathIsStale
                ? "Re-register"
                : "Register with Windows";

            StartAgentButton.Enabled = !status.AgentRunning;
            StartAgentButton.Text = status.AgentRunning ? "Agent running" : "Start agent";
        }

        private void DoRegister()
        {
            try
            {
                RegistrationService.Register();
                RefreshStatus();

                var answer = MessageBox.Show(
                    "Registered.\n\n" +
                    "Windows will not let an application make itself the default browser, so the last step " +
                    "is manual: choose \"URL Router\" for HTTP and HTTPS in Settings.\n\n" +
                    "Restart Outlook and Teams afterwards — they cache the handler at startup.\n\n" +
                    "Open Windows default apps settings now?",
                    "URL Router", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                if (answer == DialogResult.Yes)
                {
                    RegistrationService.OpenWindowsDefaultAppsSettings();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Registration failed:\n\n{ex.Message}",
                    "URL Router", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DoUnregister()
        {
            var answer = MessageBox.Show(
                "Remove the Windows registration?\n\n" +
                "Links will keep coming here until you also pick another browser in Settings.",
                "URL Router", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (answer != DialogResult.Yes) return;

            try
            {
                RegistrationService.Unregister();
                RefreshStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unregister failed:\n\n{ex.Message}",
                    "URL Router", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ------------------------------------------------------------------ updates tab

        /// <summary>
        /// Polls the feed on demand. Deliberately ignores the 20-hour cooldown and any skipped
        /// version: someone who pressed the button is asking about *now*, and being told "up to
        /// date" because of a dismissal made weeks ago would simply be a lie.
        /// </summary>
        private async Task CheckForUpdatesAsync()
        {
            CheckForUpdatesButton.Enabled = false;
            InstallUpdateButton.Enabled = false;
            ReleasePageButton.Enabled = false;
            UpdateStatusLabel.Text = "Checking…";
            UpdateNotesTextBox.Clear();

            try
            {
                // Read the feed URL out of the box rather than the saved config, so a URL typed
                // in to try can be tried without saving it first.
                var probe = new RouterConfig
                {
                    UpdateFeedUrl = UpdateFeedTextBox.Text.Trim(),
                    UpdateCheckEnabled = true,
                };

                var result = await UpdateService.CheckAsync(probe);

                // Only kept when there is genuinely something to install, so the Install button's
                // enabled state and this field can never disagree about what is on offer.
                _availableUpdate = result.IsNewer ? result : null;

                if (result.Error is not null)
                {
                    UpdateStatusLabel.Text = $"Could not check: {result.Error}";
                    UpdateStatusLabel.ForeColor = Color.FromArgb(170, 60, 0);
                    return;
                }

                _config.UpdateLastCheckUtc = DateTime.UtcNow;

                if (!result.IsNewer)
                {
                    UpdateStatusLabel.Text = $"Up to date — {result.Current} is the latest release.";
                    UpdateStatusLabel.ForeColor = Color.FromArgb(0, 110, 40);
                    return;
                }

                UpdateStatusLabel.Text = result.IsRequired
                    ? $"Version {result.Available} is available and is a required update."
                    : $"Version {result.Available} is available.";
                UpdateStatusLabel.ForeColor = SystemColors.ControlText;

                // A multiline TextBox only breaks on CRLF, and release notes arrive with whichever
                // ending the publisher used - so normalise rather than expanding "\n" blindly,
                // which would double the CR on notes that already had them.
                UpdateNotesTextBox.Text = string.IsNullOrWhiteSpace(result.Manifest?.Notes)
                    ? "(no release notes published)"
                    : result.Manifest!.Notes!.ReplaceLineEndings();

                InstallUpdateButton.Enabled = true;
                ReleasePageButton.Enabled = !string.IsNullOrWhiteSpace(result.Manifest?.ReleasePage);
            }
            finally
            {
                CheckForUpdatesButton.Enabled = true;
            }
        }

        private async Task InstallUpdateAsync()
        {
            if (_availableUpdate is null) return;

            InstallUpdateButton.Enabled = false;
            CheckForUpdatesButton.Enabled = false;

            try
            {
                var outcome = await UpdateFlow.OfferAsync(
                    this, _availableUpdate, _config, message => UpdateStatusLabel.Text = message);

                switch (outcome)
                {
                    case UpdateOutcome.Applied:
                        // Anything unsaved belongs to the old executable's session, so it is
                        // written out before this process goes away.
                        if (_dirty) SaveConfig();
                        _dirty = false;

                        // The successor is already started and is sitting on the agent mutex. If
                        // this window is hosted *by* the agent, that mutex is ours, and the
                        // successor cannot come up until this whole process exits — closing only
                        // the window would leave the machine running the renamed old executable.
                        if (AgentContext.IsRunningInThisProcess)
                        {
                            MessageBox.Show(this,
                                "Installed. URL Router is restarting on the new version.",
                                "URL Router", MessageBoxButtons.OK, MessageBoxIcon.Information);

                            Application.Exit();
                            return;
                        }

                        // Nothing restarts the agent on exit — the only routes back are the Run
                        // entry at sign-in and the Setup tab's button. A successor was started
                        // and is waiting on the mutex, but only for 20 seconds, so telling the
                        // user it "comes straight back" would be true only if they moved fast.
                        MessageBox.Show(this,
                            RegistrationService.IsAgentRunning()
                                ? "Installed. The background agent is still running the previous version.\n\n" +
                                  "Exit it from its tray icon, then click \"Start agent\" on the Setup tab. " +
                                  "It will also come up on the new version at your next sign-in."
                                : "Installed. Start the background agent from the Setup tab.",
                            "URL Router", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        Close();
                        return;

                    case UpdateOutcome.InstalledPendingRestart:
                        if (_dirty) SaveConfig();
                        _dirty = false;

                        MessageBox.Show(this,
                            "The new version is installed but could not be started automatically.\n\n" +
                            "Links keep working on the current version. Start the agent from the " +
                            "Setup tab, or sign out and back in.",
                            "URL Router", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                        UpdateStatusLabel.Text = "Installed — restart still needed.";
                        _availableUpdate = null;
                        return;

                    case UpdateOutcome.Skipped:
                        UpdateStatusLabel.Text = $"Version {_availableUpdate.Available} skipped.";
                        _availableUpdate = null;
                        UpdateNotesTextBox.Clear();
                        return;

                    default:
                        UpdateStatusLabel.Text = "Not installed.";
                        return;
                }
            }
            finally
            {
                CheckForUpdatesButton.Enabled = true;
                InstallUpdateButton.Enabled = _availableUpdate is not null;
            }
        }

        private void RefreshLog()
        {
            var lines = RouterLog.ReadRecent();
            LogTextBox.Text = lines.Count == 0
                ? "(nothing logged yet)"
                : string.Join(Environment.NewLine, lines);
        }

        private void OpenConfigFolder()
        {
            try
            {
                Directory.CreateDirectory(ConfigService.ConfigDirectory);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{ConfigService.ConfigDirectory}\""));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "URL Router", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ------------------------------------------------------------------ save

        private bool SaveConfig()
        {
            RulesGrid.EndEdit();
            TargetsGrid.EndEdit();

            var blank = _config.Rules.Where(r => string.IsNullOrWhiteSpace(r.HostPattern)).ToList();
            if (blank.Count > 0)
            {
                MessageBox.Show($"{blank.Count} rule(s) have no host pattern. Fill them in or delete them.",
                    "URL Router", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            var selected = FallbackCombo.SelectedValue as string;
            _config.FallbackTargetId = string.IsNullOrEmpty(selected) ? null : selected;
            _config.LogEnabled = LogEnabledCheckBox.Checked;
            _config.UnwrapSafeLinks = UnwrapCheckBox.Checked;

            _config.UpdateCheckEnabled = UpdateCheckEnabledCheckBox.Checked;

            var feed = UpdateFeedTextBox.Text.Trim();
            _config.UpdateFeedUrl = feed.Length == 0 ? RouterConfig.DefaultUpdateFeedUrl : feed;

            try
            {
                ConfigService.Save(_config);
                _dirty = false;
                Text = "URL Router";
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save:\n\n{ex.Message}",
                    "URL Router", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void ConfigForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!_dirty) return;

            var answer = MessageBox.Show("Save changes before closing?",
                "URL Router", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            switch (answer)
            {
                case DialogResult.Yes:
                    if (!SaveConfig()) e.Cancel = true;
                    break;
                case DialogResult.Cancel:
                    e.Cancel = true;
                    break;
            }
        }
    }
}
