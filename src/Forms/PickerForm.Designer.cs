namespace UrlRouter.Forms
{
    partial class PickerForm
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
            PromptLabel = new Label();
            UrlLabel = new Label();
            TargetsPanel = new FlowLayoutPanel();
            AlwaysCheckBox = new CheckBox();
            CancelPickerButton = new Button();
            SuspendLayout();
            //
            // PromptLabel
            //
            PromptLabel.AutoSize = true;
            PromptLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            PromptLabel.Location = new Point(14, 12);
            PromptLabel.Name = "PromptLabel";
            PromptLabel.Size = new Size(120, 15);
            PromptLabel.TabIndex = 0;
            PromptLabel.Text = "Open this link in:";
            //
            // UrlLabel
            //
            UrlLabel.AutoEllipsis = true;
            UrlLabel.ForeColor = SystemColors.GrayText;
            UrlLabel.Location = new Point(14, 32);
            UrlLabel.Name = "UrlLabel";
            UrlLabel.Size = new Size(432, 34);
            UrlLabel.TabIndex = 1;
            UrlLabel.Text = "https://";
            //
            // TargetsPanel
            //
            // Height is set explicitly in code once the target count is known - AutoSize
            // would fight that arithmetic.
            TargetsPanel.AutoSize = false;
            TargetsPanel.FlowDirection = FlowDirection.TopDown;
            TargetsPanel.Location = new Point(14, 72);
            TargetsPanel.Name = "TargetsPanel";
            TargetsPanel.Size = new Size(432, 40);
            TargetsPanel.TabIndex = 2;
            TargetsPanel.WrapContents = false;
            //
            // AlwaysCheckBox
            //
            AlwaysCheckBox.AutoSize = true;
            AlwaysCheckBox.Location = new Point(16, 124);
            AlwaysCheckBox.Name = "AlwaysCheckBox";
            AlwaysCheckBox.Size = new Size(200, 19);
            AlwaysCheckBox.TabIndex = 3;
            AlwaysCheckBox.Text = "Always use this for this site";
            AlwaysCheckBox.UseVisualStyleBackColor = true;
            //
            // CancelPickerButton
            //
            CancelPickerButton.DialogResult = DialogResult.Cancel;
            CancelPickerButton.Location = new Point(371, 120);
            CancelPickerButton.Name = "CancelPickerButton";
            CancelPickerButton.Size = new Size(75, 25);
            CancelPickerButton.TabIndex = 4;
            CancelPickerButton.Text = "Cancel";
            CancelPickerButton.UseVisualStyleBackColor = true;
            //
            // PickerForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = CancelPickerButton;
            ClientSize = new Size(460, 158);
            Controls.Add(CancelPickerButton);
            Controls.Add(AlwaysCheckBox);
            Controls.Add(TargetsPanel);
            Controls.Add(UrlLabel);
            Controls.Add(PromptLabel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            KeyPreview = true;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "PickerForm";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "URL Router";
            TopMost = true;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label PromptLabel;
        private Label UrlLabel;
        private FlowLayoutPanel TargetsPanel;
        private CheckBox AlwaysCheckBox;
        private Button CancelPickerButton;
    }
}
