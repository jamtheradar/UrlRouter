namespace UrlRouter.Forms
{
    partial class ConfigForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            Tabs = new TabControl();
            RulesTab = new TabPage();
            RulesGrid = new DataGridView();
            AddRuleButton = new Button();
            DeleteRuleButton = new Button();
            MoveUpButton = new Button();
            MoveDownButton = new Button();
            RulesHintLabel = new Label();
            BrowsersTab = new TabPage();
            TargetsGrid = new DataGridView();
            AddTargetButton = new Button();
            DeleteTargetButton = new Button();
            RedetectButton = new Button();
            TargetsHintLabel = new Label();
            TestTab = new TabPage();
            TestUrlLabel = new Label();
            TestUrlTextBox = new TextBox();
            TestButton = new Button();
            TestLaunchButton = new Button();
            TestResultTextBox = new TextBox();
            SetupTab = new TabPage();
            StatusTitleLabel = new Label();
            StatusLabel = new Label();
            RegisterButton = new Button();
            UnregisterButton = new Button();
            DefaultAppsButton = new Button();
            RefreshStatusButton = new Button();
            StartAgentButton = new Button();
            FallbackLabel = new Label();
            FallbackCombo = new ComboBox();
            LogEnabledCheckBox = new CheckBox();
            UnwrapCheckBox = new CheckBox();
            LogTitleLabel = new Label();
            LogTextBox = new TextBox();
            RefreshLogButton = new Button();
            OpenLogFolderButton = new Button();
            UpdatesTab = new TabPage();
            VersionTitleLabel = new Label();
            VersionLabel = new Label();
            UpdateCheckEnabledCheckBox = new CheckBox();
            UpdateFeedLabel = new Label();
            UpdateFeedTextBox = new TextBox();
            CheckForUpdatesButton = new Button();
            InstallUpdateButton = new Button();
            ReleasePageButton = new Button();
            UpdateStatusLabel = new Label();
            UpdateNotesTextBox = new TextBox();
            SaveButton = new Button();
            CloseFormButton = new Button();
            ConfigPathLabel = new Label();
            Tabs.SuspendLayout();
            RulesTab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)RulesGrid).BeginInit();
            BrowsersTab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)TargetsGrid).BeginInit();
            TestTab.SuspendLayout();
            SetupTab.SuspendLayout();
            UpdatesTab.SuspendLayout();
            SuspendLayout();
            //
            // Tabs
            //
            Tabs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Tabs.Controls.Add(RulesTab);
            Tabs.Controls.Add(BrowsersTab);
            Tabs.Controls.Add(TestTab);
            Tabs.Controls.Add(SetupTab);
            Tabs.Controls.Add(UpdatesTab);
            Tabs.Location = new Point(8, 8);
            Tabs.Name = "Tabs";
            Tabs.SelectedIndex = 0;
            Tabs.Size = new Size(884, 552);
            Tabs.TabIndex = 0;
            //
            // RulesTab
            //
            RulesTab.Controls.Add(RulesGrid);
            RulesTab.Controls.Add(AddRuleButton);
            RulesTab.Controls.Add(DeleteRuleButton);
            RulesTab.Controls.Add(MoveUpButton);
            RulesTab.Controls.Add(MoveDownButton);
            RulesTab.Controls.Add(RulesHintLabel);
            RulesTab.Location = new Point(4, 24);
            RulesTab.Name = "RulesTab";
            RulesTab.Padding = new Padding(3);
            RulesTab.Size = new Size(876, 524);
            RulesTab.TabIndex = 0;
            RulesTab.Text = "Rules";
            RulesTab.UseVisualStyleBackColor = true;
            //
            // RulesGrid
            //
            RulesGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            RulesGrid.AllowUserToAddRows = false;
            RulesGrid.AllowUserToResizeRows = false;
            RulesGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            RulesGrid.Location = new Point(8, 8);
            RulesGrid.MultiSelect = false;
            RulesGrid.Name = "RulesGrid";
            RulesGrid.RowHeadersWidth = 28;
            RulesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            RulesGrid.Size = new Size(748, 468);
            RulesGrid.TabIndex = 0;
            //
            // AddRuleButton
            //
            AddRuleButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            AddRuleButton.Location = new Point(764, 8);
            AddRuleButton.Name = "AddRuleButton";
            AddRuleButton.Size = new Size(100, 27);
            AddRuleButton.TabIndex = 1;
            AddRuleButton.Text = "Add";
            AddRuleButton.UseVisualStyleBackColor = true;
            //
            // DeleteRuleButton
            //
            DeleteRuleButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            DeleteRuleButton.Location = new Point(764, 41);
            DeleteRuleButton.Name = "DeleteRuleButton";
            DeleteRuleButton.Size = new Size(100, 27);
            DeleteRuleButton.TabIndex = 2;
            DeleteRuleButton.Text = "Delete";
            DeleteRuleButton.UseVisualStyleBackColor = true;
            //
            // MoveUpButton
            //
            MoveUpButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            MoveUpButton.Location = new Point(764, 82);
            MoveUpButton.Name = "MoveUpButton";
            MoveUpButton.Size = new Size(100, 27);
            MoveUpButton.TabIndex = 3;
            MoveUpButton.Text = "Move up";
            MoveUpButton.UseVisualStyleBackColor = true;
            //
            // MoveDownButton
            //
            MoveDownButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            MoveDownButton.Location = new Point(764, 115);
            MoveDownButton.Name = "MoveDownButton";
            MoveDownButton.Size = new Size(100, 27);
            MoveDownButton.TabIndex = 4;
            MoveDownButton.Text = "Move down";
            MoveDownButton.UseVisualStyleBackColor = true;
            //
            // RulesHintLabel
            //
            RulesHintLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            RulesHintLabel.ForeColor = SystemColors.GrayText;
            RulesHintLabel.Location = new Point(8, 482);
            RulesHintLabel.Name = "RulesHintLabel";
            RulesHintLabel.Size = new Size(856, 34);
            RulesHintLabel.TabIndex = 5;
            RulesHintLabel.Text = "Host patterns use * as a wildcard, e.g. *.gmail.com.au (which also matches gmail.com). Path is optional, e.g. /browse/ISD-*.\r\nThe first enabled rule that matches wins — use Move up / Move down to order them.";
            //
            // BrowsersTab
            //
            BrowsersTab.Controls.Add(TargetsGrid);
            BrowsersTab.Controls.Add(AddTargetButton);
            BrowsersTab.Controls.Add(DeleteTargetButton);
            BrowsersTab.Controls.Add(RedetectButton);
            BrowsersTab.Controls.Add(TargetsHintLabel);
            BrowsersTab.Location = new Point(4, 24);
            BrowsersTab.Name = "BrowsersTab";
            BrowsersTab.Padding = new Padding(3);
            BrowsersTab.Size = new Size(876, 524);
            BrowsersTab.TabIndex = 1;
            BrowsersTab.Text = "Browsers";
            BrowsersTab.UseVisualStyleBackColor = true;
            //
            // TargetsGrid
            //
            TargetsGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            TargetsGrid.AllowUserToAddRows = false;
            TargetsGrid.AllowUserToResizeRows = false;
            TargetsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            TargetsGrid.Location = new Point(8, 8);
            TargetsGrid.MultiSelect = false;
            TargetsGrid.Name = "TargetsGrid";
            TargetsGrid.RowHeadersWidth = 28;
            TargetsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            TargetsGrid.Size = new Size(748, 468);
            TargetsGrid.TabIndex = 0;
            //
            // AddTargetButton
            //
            AddTargetButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            AddTargetButton.Location = new Point(764, 8);
            AddTargetButton.Name = "AddTargetButton";
            AddTargetButton.Size = new Size(100, 27);
            AddTargetButton.TabIndex = 1;
            AddTargetButton.Text = "Add…";
            AddTargetButton.UseVisualStyleBackColor = true;
            //
            // DeleteTargetButton
            //
            DeleteTargetButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            DeleteTargetButton.Location = new Point(764, 41);
            DeleteTargetButton.Name = "DeleteTargetButton";
            DeleteTargetButton.Size = new Size(100, 27);
            DeleteTargetButton.TabIndex = 2;
            DeleteTargetButton.Text = "Delete";
            DeleteTargetButton.UseVisualStyleBackColor = true;
            //
            // RedetectButton
            //
            RedetectButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            RedetectButton.Location = new Point(764, 82);
            RedetectButton.Name = "RedetectButton";
            RedetectButton.Size = new Size(100, 27);
            RedetectButton.TabIndex = 3;
            RedetectButton.Text = "Re-detect";
            RedetectButton.UseVisualStyleBackColor = true;
            //
            // TargetsHintLabel
            //
            TargetsHintLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            TargetsHintLabel.ForeColor = SystemColors.GrayText;
            TargetsHintLabel.Location = new Point(8, 482);
            TargetsHintLabel.Name = "TargetsHintLabel";
            TargetsHintLabel.Size = new Size(856, 34);
            TargetsHintLabel.TabIndex = 4;
            TargetsHintLabel.Text = "Profile is the Chromium --profile-directory value (Default, Profile 1, …). Leave it blank for browsers that do not use profiles.\r\nRe-detect adds newly found profiles without touching rows you have renamed or added yourself.";
            //
            // TestTab
            //
            TestTab.Controls.Add(TestUrlLabel);
            TestTab.Controls.Add(TestUrlTextBox);
            TestTab.Controls.Add(TestButton);
            TestTab.Controls.Add(TestLaunchButton);
            TestTab.Controls.Add(TestResultTextBox);
            TestTab.Location = new Point(4, 24);
            TestTab.Name = "TestTab";
            TestTab.Padding = new Padding(3);
            TestTab.Size = new Size(876, 524);
            TestTab.TabIndex = 2;
            TestTab.Text = "Test";
            TestTab.UseVisualStyleBackColor = true;
            //
            // TestUrlLabel
            //
            TestUrlLabel.AutoSize = true;
            TestUrlLabel.Location = new Point(8, 12);
            TestUrlLabel.Name = "TestUrlLabel";
            TestUrlLabel.Size = new Size(300, 15);
            TestUrlLabel.TabIndex = 0;
            TestUrlLabel.Text = "Paste a URL to see where it would open (nothing is opened):";
            //
            // TestUrlTextBox
            //
            TestUrlTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            TestUrlTextBox.Location = new Point(8, 32);
            TestUrlTextBox.Name = "TestUrlTextBox";
            TestUrlTextBox.Size = new Size(748, 23);
            TestUrlTextBox.TabIndex = 1;
            //
            // TestButton
            //
            TestButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            TestButton.Location = new Point(764, 31);
            TestButton.Name = "TestButton";
            TestButton.Size = new Size(100, 25);
            TestButton.TabIndex = 2;
            TestButton.Text = "Test";
            TestButton.UseVisualStyleBackColor = true;
            //
            // TestLaunchButton
            //
            TestLaunchButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            TestLaunchButton.Location = new Point(764, 62);
            TestLaunchButton.Name = "TestLaunchButton";
            TestLaunchButton.Size = new Size(100, 25);
            TestLaunchButton.TabIndex = 3;
            TestLaunchButton.Text = "Open it";
            TestLaunchButton.UseVisualStyleBackColor = true;
            //
            // TestResultTextBox
            //
            TestResultTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            TestResultTextBox.Font = new Font("Consolas", 9F);
            TestResultTextBox.Location = new Point(8, 96);
            TestResultTextBox.Multiline = true;
            TestResultTextBox.Name = "TestResultTextBox";
            TestResultTextBox.ReadOnly = true;
            TestResultTextBox.ScrollBars = ScrollBars.Vertical;
            TestResultTextBox.Size = new Size(856, 420);
            TestResultTextBox.TabIndex = 4;
            //
            // SetupTab
            //
            SetupTab.Controls.Add(StatusTitleLabel);
            SetupTab.Controls.Add(StatusLabel);
            SetupTab.Controls.Add(RegisterButton);
            SetupTab.Controls.Add(UnregisterButton);
            SetupTab.Controls.Add(DefaultAppsButton);
            SetupTab.Controls.Add(RefreshStatusButton);
            SetupTab.Controls.Add(StartAgentButton);
            SetupTab.Controls.Add(FallbackLabel);
            SetupTab.Controls.Add(FallbackCombo);
            SetupTab.Controls.Add(LogEnabledCheckBox);
            SetupTab.Controls.Add(UnwrapCheckBox);
            SetupTab.Controls.Add(LogTitleLabel);
            SetupTab.Controls.Add(LogTextBox);
            SetupTab.Controls.Add(RefreshLogButton);
            SetupTab.Controls.Add(OpenLogFolderButton);
            SetupTab.Location = new Point(4, 24);
            SetupTab.Name = "SetupTab";
            SetupTab.Padding = new Padding(3);
            SetupTab.Size = new Size(876, 524);
            SetupTab.TabIndex = 3;
            SetupTab.Text = "Setup";
            SetupTab.UseVisualStyleBackColor = true;
            //
            // StatusTitleLabel
            //
            StatusTitleLabel.AutoSize = true;
            StatusTitleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            StatusTitleLabel.Location = new Point(8, 12);
            StatusTitleLabel.Name = "StatusTitleLabel";
            StatusTitleLabel.Size = new Size(140, 15);
            StatusTitleLabel.TabIndex = 0;
            StatusTitleLabel.Text = "Windows registration";
            //
            // StatusLabel
            //
            StatusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            StatusLabel.Location = new Point(8, 32);
            StatusLabel.Name = "StatusLabel";
            StatusLabel.Size = new Size(856, 50);
            StatusLabel.TabIndex = 1;
            StatusLabel.Text = "…";
            //
            // RegisterButton
            //
            RegisterButton.Location = new Point(8, 88);
            RegisterButton.Name = "RegisterButton";
            RegisterButton.Size = new Size(150, 28);
            RegisterButton.TabIndex = 2;
            RegisterButton.Text = "Register with Windows";
            RegisterButton.UseVisualStyleBackColor = true;
            //
            // UnregisterButton
            //
            UnregisterButton.Location = new Point(166, 88);
            UnregisterButton.Name = "UnregisterButton";
            UnregisterButton.Size = new Size(110, 28);
            UnregisterButton.TabIndex = 3;
            UnregisterButton.Text = "Unregister";
            UnregisterButton.UseVisualStyleBackColor = true;
            //
            // DefaultAppsButton
            //
            DefaultAppsButton.Location = new Point(284, 88);
            DefaultAppsButton.Name = "DefaultAppsButton";
            DefaultAppsButton.Size = new Size(210, 28);
            DefaultAppsButton.TabIndex = 4;
            DefaultAppsButton.Text = "Open Windows default apps…";
            DefaultAppsButton.UseVisualStyleBackColor = true;
            //
            // RefreshStatusButton
            //
            RefreshStatusButton.Location = new Point(502, 88);
            RefreshStatusButton.Name = "RefreshStatusButton";
            RefreshStatusButton.Size = new Size(100, 28);
            RefreshStatusButton.TabIndex = 5;
            RefreshStatusButton.Text = "Refresh";
            RefreshStatusButton.UseVisualStyleBackColor = true;
            //
            // StartAgentButton
            //
            StartAgentButton.Location = new Point(610, 88);
            StartAgentButton.Name = "StartAgentButton";
            StartAgentButton.Size = new Size(110, 28);
            StartAgentButton.TabIndex = 14;
            StartAgentButton.Text = "Start agent";
            StartAgentButton.UseVisualStyleBackColor = true;
            //
            // FallbackLabel
            //
            FallbackLabel.AutoSize = true;
            FallbackLabel.Location = new Point(8, 134);
            FallbackLabel.Name = "FallbackLabel";
            FallbackLabel.Size = new Size(220, 15);
            FallbackLabel.TabIndex = 6;
            FallbackLabel.Text = "When no rule matches, open links in:";
            //
            // FallbackCombo
            //
            FallbackCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            FallbackCombo.Location = new Point(8, 154);
            FallbackCombo.Name = "FallbackCombo";
            FallbackCombo.Size = new Size(320, 23);
            FallbackCombo.TabIndex = 7;
            //
            // LogEnabledCheckBox
            //
            LogEnabledCheckBox.AutoSize = true;
            LogEnabledCheckBox.Location = new Point(8, 192);
            LogEnabledCheckBox.Name = "LogEnabledCheckBox";
            LogEnabledCheckBox.Size = new Size(300, 19);
            LogEnabledCheckBox.TabIndex = 8;
            LogEnabledCheckBox.Text = "Log every routing decision (useful when a link goes astray)";
            LogEnabledCheckBox.UseVisualStyleBackColor = true;
            //
            // UnwrapCheckBox
            //
            UnwrapCheckBox.AutoSize = true;
            UnwrapCheckBox.Location = new Point(8, 216);
            UnwrapCheckBox.Name = "UnwrapCheckBox";
            UnwrapCheckBox.Size = new Size(400, 19);
            UnwrapCheckBox.TabIndex = 9;
            UnwrapCheckBox.Text = "Unwrap Outlook / Teams Safe Links before matching (leave this on)";
            UnwrapCheckBox.UseVisualStyleBackColor = true;
            //
            // LogTitleLabel
            //
            LogTitleLabel.AutoSize = true;
            LogTitleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            LogTitleLabel.Location = new Point(8, 250);
            LogTitleLabel.Name = "LogTitleLabel";
            LogTitleLabel.Size = new Size(100, 15);
            LogTitleLabel.TabIndex = 10;
            LogTitleLabel.Text = "Recent activity";
            //
            // LogTextBox
            //
            LogTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            LogTextBox.Font = new Font("Consolas", 9F);
            LogTextBox.Location = new Point(8, 270);
            LogTextBox.Multiline = true;
            LogTextBox.Name = "LogTextBox";
            LogTextBox.ReadOnly = true;
            LogTextBox.ScrollBars = ScrollBars.Both;
            LogTextBox.Size = new Size(856, 212);
            LogTextBox.TabIndex = 11;
            LogTextBox.WordWrap = false;
            //
            // RefreshLogButton
            //
            RefreshLogButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            RefreshLogButton.Location = new Point(8, 490);
            RefreshLogButton.Name = "RefreshLogButton";
            RefreshLogButton.Size = new Size(110, 27);
            RefreshLogButton.TabIndex = 12;
            RefreshLogButton.Text = "Refresh log";
            RefreshLogButton.UseVisualStyleBackColor = true;
            //
            // OpenLogFolderButton
            //
            OpenLogFolderButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            OpenLogFolderButton.Location = new Point(126, 490);
            OpenLogFolderButton.Name = "OpenLogFolderButton";
            OpenLogFolderButton.Size = new Size(140, 27);
            OpenLogFolderButton.TabIndex = 13;
            OpenLogFolderButton.Text = "Open config folder";
            OpenLogFolderButton.UseVisualStyleBackColor = true;
            //
            // SaveButton
            //
            SaveButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            SaveButton.Location = new Point(700, 568);
            SaveButton.Name = "SaveButton";
            SaveButton.Size = new Size(90, 28);
            SaveButton.TabIndex = 1;
            SaveButton.Text = "Save";
            SaveButton.UseVisualStyleBackColor = true;
            //
            // CloseFormButton
            //
            CloseFormButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            CloseFormButton.Location = new Point(798, 568);
            CloseFormButton.Name = "CloseFormButton";
            CloseFormButton.Size = new Size(90, 28);
            CloseFormButton.TabIndex = 2;
            CloseFormButton.Text = "Close";
            CloseFormButton.UseVisualStyleBackColor = true;
            //
            // ConfigPathLabel
            //
            ConfigPathLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            ConfigPathLabel.AutoEllipsis = true;
            ConfigPathLabel.ForeColor = SystemColors.GrayText;
            ConfigPathLabel.Location = new Point(10, 574);
            ConfigPathLabel.Name = "ConfigPathLabel";
            ConfigPathLabel.Size = new Size(680, 18);
            ConfigPathLabel.TabIndex = 3;
            ConfigPathLabel.Text = "…";
            //
            // UpdatesTab
            //
            UpdatesTab.Controls.Add(VersionTitleLabel);
            UpdatesTab.Controls.Add(VersionLabel);
            UpdatesTab.Controls.Add(UpdateCheckEnabledCheckBox);
            UpdatesTab.Controls.Add(CheckForUpdatesButton);
            UpdatesTab.Controls.Add(InstallUpdateButton);
            UpdatesTab.Controls.Add(ReleasePageButton);
            UpdatesTab.Controls.Add(UpdateStatusLabel);
            UpdatesTab.Controls.Add(UpdateNotesTextBox);
            UpdatesTab.Controls.Add(UpdateFeedLabel);
            UpdatesTab.Controls.Add(UpdateFeedTextBox);
            UpdatesTab.Location = new Point(4, 24);
            UpdatesTab.Name = "UpdatesTab";
            UpdatesTab.Padding = new Padding(3);
            UpdatesTab.Size = new Size(876, 524);
            UpdatesTab.TabIndex = 4;
            UpdatesTab.Text = "Updates";
            UpdatesTab.UseVisualStyleBackColor = true;
            //
            // VersionTitleLabel
            //
            VersionTitleLabel.AutoSize = true;
            VersionTitleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            VersionTitleLabel.Location = new Point(8, 12);
            VersionTitleLabel.Name = "VersionTitleLabel";
            VersionTitleLabel.Size = new Size(110, 15);
            VersionTitleLabel.TabIndex = 0;
            VersionTitleLabel.Text = "Installed version";
            //
            // VersionLabel
            //
            VersionLabel.AutoSize = true;
            VersionLabel.Location = new Point(8, 32);
            VersionLabel.Name = "VersionLabel";
            VersionLabel.Size = new Size(200, 15);
            VersionLabel.TabIndex = 1;
            VersionLabel.Text = "…";
            //
            // UpdateCheckEnabledCheckBox
            //
            UpdateCheckEnabledCheckBox.AutoSize = true;
            UpdateCheckEnabledCheckBox.Location = new Point(8, 62);
            UpdateCheckEnabledCheckBox.Name = "UpdateCheckEnabledCheckBox";
            UpdateCheckEnabledCheckBox.Size = new Size(420, 19);
            UpdateCheckEnabledCheckBox.TabIndex = 2;
            UpdateCheckEnabledCheckBox.Text = "Check for new versions in the background (nothing installs without asking)";
            UpdateCheckEnabledCheckBox.UseVisualStyleBackColor = true;
            //
            // CheckForUpdatesButton
            //
            CheckForUpdatesButton.Location = new Point(8, 94);
            CheckForUpdatesButton.Name = "CheckForUpdatesButton";
            CheckForUpdatesButton.Size = new Size(150, 28);
            CheckForUpdatesButton.TabIndex = 3;
            CheckForUpdatesButton.Text = "Check now";
            CheckForUpdatesButton.UseVisualStyleBackColor = true;
            //
            // InstallUpdateButton
            //
            InstallUpdateButton.Enabled = false;
            InstallUpdateButton.Location = new Point(166, 94);
            InstallUpdateButton.Name = "InstallUpdateButton";
            InstallUpdateButton.Size = new Size(180, 28);
            InstallUpdateButton.TabIndex = 4;
            InstallUpdateButton.Text = "Download and install";
            InstallUpdateButton.UseVisualStyleBackColor = true;
            //
            // ReleasePageButton
            //
            ReleasePageButton.Enabled = false;
            ReleasePageButton.Location = new Point(354, 94);
            ReleasePageButton.Name = "ReleasePageButton";
            ReleasePageButton.Size = new Size(160, 28);
            ReleasePageButton.TabIndex = 5;
            ReleasePageButton.Text = "Open release page";
            ReleasePageButton.UseVisualStyleBackColor = true;
            //
            // UpdateStatusLabel
            //
            UpdateStatusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            UpdateStatusLabel.Location = new Point(8, 132);
            UpdateStatusLabel.Name = "UpdateStatusLabel";
            UpdateStatusLabel.Size = new Size(856, 36);
            UpdateStatusLabel.TabIndex = 6;
            UpdateStatusLabel.Text = "…";
            //
            // UpdateNotesTextBox
            //
            UpdateNotesTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            UpdateNotesTextBox.Location = new Point(8, 176);
            UpdateNotesTextBox.Multiline = true;
            UpdateNotesTextBox.Name = "UpdateNotesTextBox";
            UpdateNotesTextBox.ReadOnly = true;
            UpdateNotesTextBox.ScrollBars = ScrollBars.Vertical;
            UpdateNotesTextBox.Size = new Size(856, 268);
            UpdateNotesTextBox.TabIndex = 7;
            //
            // UpdateFeedLabel
            //
            UpdateFeedLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            UpdateFeedLabel.AutoSize = true;
            UpdateFeedLabel.ForeColor = SystemColors.GrayText;
            UpdateFeedLabel.Location = new Point(8, 456);
            UpdateFeedLabel.Name = "UpdateFeedLabel";
            UpdateFeedLabel.Size = new Size(70, 15);
            UpdateFeedLabel.TabIndex = 8;
            UpdateFeedLabel.Text = "Update feed";
            //
            // UpdateFeedTextBox
            //
            UpdateFeedTextBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            UpdateFeedTextBox.Location = new Point(8, 474);
            UpdateFeedTextBox.Name = "UpdateFeedTextBox";
            UpdateFeedTextBox.Size = new Size(856, 23);
            UpdateFeedTextBox.TabIndex = 9;
            //
            // ConfigForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(900, 604);
            Controls.Add(Tabs);
            Controls.Add(SaveButton);
            Controls.Add(CloseFormButton);
            Controls.Add(ConfigPathLabel);
            MinimumSize = new Size(760, 520);
            Name = "ConfigForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "URL Router";
            Tabs.ResumeLayout(false);
            RulesTab.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)RulesGrid).EndInit();
            BrowsersTab.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)TargetsGrid).EndInit();
            TestTab.ResumeLayout(false);
            TestTab.PerformLayout();
            SetupTab.ResumeLayout(false);
            SetupTab.PerformLayout();
            UpdatesTab.ResumeLayout(false);
            UpdatesTab.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private TabControl Tabs;
        private TabPage RulesTab;
        private DataGridView RulesGrid;
        private Button AddRuleButton;
        private Button DeleteRuleButton;
        private Button MoveUpButton;
        private Button MoveDownButton;
        private Label RulesHintLabel;
        private TabPage BrowsersTab;
        private DataGridView TargetsGrid;
        private Button AddTargetButton;
        private Button DeleteTargetButton;
        private Button RedetectButton;
        private Label TargetsHintLabel;
        private TabPage TestTab;
        private Label TestUrlLabel;
        private TextBox TestUrlTextBox;
        private Button TestButton;
        private Button TestLaunchButton;
        private TextBox TestResultTextBox;
        private TabPage SetupTab;
        private Label StatusTitleLabel;
        private Label StatusLabel;
        private Button RegisterButton;
        private Button UnregisterButton;
        private Button DefaultAppsButton;
        private Button RefreshStatusButton;
        private Button StartAgentButton;
        private Label FallbackLabel;
        private ComboBox FallbackCombo;
        private CheckBox LogEnabledCheckBox;
        private CheckBox UnwrapCheckBox;
        private Label LogTitleLabel;
        private TextBox LogTextBox;
        private Button RefreshLogButton;
        private Button OpenLogFolderButton;
        private TabPage UpdatesTab;
        private Label VersionTitleLabel;
        private Label VersionLabel;
        private CheckBox UpdateCheckEnabledCheckBox;
        private Label UpdateFeedLabel;
        private TextBox UpdateFeedTextBox;
        private Button CheckForUpdatesButton;
        private Button InstallUpdateButton;
        private Button ReleasePageButton;
        private Label UpdateStatusLabel;
        private TextBox UpdateNotesTextBox;
        private Button SaveButton;
        private Button CloseFormButton;
        private Label ConfigPathLabel;
    }
}
