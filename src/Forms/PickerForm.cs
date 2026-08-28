using UrlRouter.Models;
using UrlRouter.Services;

namespace UrlRouter.Forms
{
    /// <summary>
    /// Shown when no rule matches. Rather than guessing a browser, it asks - and offers to
    /// remember the answer, so the rule set grows out of normal use instead of needing to be
    /// written up front.
    /// </summary>
    public partial class PickerForm : Form
    {
        private readonly RouterConfig _config;
        private readonly Uri _uri;

        private BrowserTarget? _chosen;

        public PickerForm(RouterConfig config, Uri uri, string displayUrl)
        {
            _config = config;
            _uri = uri;

            InitializeComponent();

            UrlLabel.Text = displayUrl;
            AlwaysCheckBox.Text = $"Always use this for {uri.Host}";

            BuildTargetButtons();
            PositionOnCursorScreen();
        }

        /// <summary>
        /// Runs the picker and returns the chosen target, or null if cancelled. When the
        /// user asks to remember the choice, a rule is appended and the config saved before
        /// returning.
        /// </summary>
        public static BrowserTarget? Choose(RouterConfig config, Uri uri, string displayUrl)
        {
            if (config.Targets.Count == 0)
            {
                MessageBox.Show(
                    "No browsers are configured yet.\n\nRun UrlRouter --config and use Re-detect on the Browsers tab.",
                    "URL Router", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            using var form = new PickerForm(config, uri, displayUrl);
            form.ShowDialog();
            return form._chosen;
        }

        private const int ButtonHeight = 30;
        private const int ButtonSpacing = 4;

        private void BuildTargetButtons()
        {
            TargetsPanel.Controls.Clear();

            var index = 1;
            foreach (var target in _config.Targets)
            {
                var button = new Button
                {
                    // The number prefix pairs with the 1-9 shortcut handled in OnKeyDown.
                    Text = index <= 9 ? $"{index}   {target.DisplayName}" : $"     {target.DisplayName}",
                    TextAlign = ContentAlignment.MiddleLeft,
                    Width = TargetsPanel.Width - 6,
                    Height = ButtonHeight,
                    Margin = new Padding(0, 0, 0, ButtonSpacing),
                    Tag = target,
                    UseVisualStyleBackColor = true,
                };

                button.Click += (_, _) => Accept(target);
                TargetsPanel.Controls.Add(button);
                index++;
            }

            // Grow the window to fit however many browsers are configured, and move
            // everything below the list down by the same amount.
            var listHeight = TargetsPanel.Controls.Count * (ButtonHeight + ButtonSpacing);
            var delta = listHeight - TargetsPanel.Height;

            TargetsPanel.Height = listHeight;
            Height += delta;
            AlwaysCheckBox.Top += delta;
            CancelPickerButton.Top += delta;

            // With many profiles configured the list could otherwise run off-screen.
            var maxHeight = Screen.FromPoint(Cursor.Position).WorkingArea.Height - 80;
            if (Height > maxHeight)
            {
                TargetsPanel.AutoScroll = true;
                TargetsPanel.Height -= Height - maxHeight;
                AlwaysCheckBox.Top -= Height - maxHeight;
                CancelPickerButton.Top -= Height - maxHeight;
                Height = maxHeight;
            }

            if (TargetsPanel.Controls.Count > 0)
            {
                ActiveControl = TargetsPanel.Controls[0];
            }
        }

        /// <summary>
        /// Opens on whichever monitor the mouse is on. A link click always follows a click,
        /// so the cursor is a reliable proxy for where the user is looking.
        /// </summary>
        private void PositionOnCursorScreen()
        {
            var screen = Screen.FromPoint(Cursor.Position);
            var area = screen.WorkingArea;

            Location = new Point(
                area.Left + (area.Width - Width) / 2,
                area.Top + (area.Height - Height) / 2);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // Windows will not focus a window from a background process by default; without
            // this the picker can appear behind Outlook and look like nothing happened.
            Activate();
            BringToFront();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9)
            {
                SelectByNumber(e.KeyCode - Keys.D1);
                e.Handled = true;
                return;
            }

            if (e.KeyCode >= Keys.NumPad1 && e.KeyCode <= Keys.NumPad9)
            {
                SelectByNumber(e.KeyCode - Keys.NumPad1);
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }

        private void SelectByNumber(int index)
        {
            if (index < 0 || index >= _config.Targets.Count) return;
            Accept(_config.Targets[index]);
        }

        private void Accept(BrowserTarget target)
        {
            _chosen = target;

            if (AlwaysCheckBox.Checked)
            {
                RememberChoice(target);
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>
        /// Appends a rule for this exact host. Appended rather than inserted so it cannot
        /// shadow more specific rules the user has already ordered by hand.
        /// </summary>
        private void RememberChoice(BrowserTarget target)
        {
            try
            {
                _config.Rules.Add(new RoutingRule
                {
                    Enabled = true,
                    HostPattern = _uri.Host,
                    TargetId = target.Id,
                    Comment = $"Added from picker {DateTime.Now:yyyy-MM-dd}",
                });

                ConfigService.Save(_config);
                RouterLog.Write($"rule added from picker: {_uri.Host} -> {target.DisplayName}");
            }
            catch (Exception ex)
            {
                // The link should still open even if the rule could not be persisted.
                RouterLog.Write($"could not save picker rule: {ex.Message}");
                MessageBox.Show($"The link will still open, but the rule could not be saved:\n\n{ex.Message}",
                    "URL Router", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
