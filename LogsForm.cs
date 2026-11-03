using System;
using System.Drawing;
using System.Windows.Forms;

namespace Rapid_Downloader
{
    public class LogsForm : Form
    {
        private TextBox txtLogs = null!;
        private Button btnCopy = null!;
        private Button btnClear = null!;
        private Button btnClose = null!;

        public LogsForm(Icon? appIcon)
        {
            InitializeComponent(appIcon);
            txtLogs.Text = AppLogger.GetAllLogs();
            txtLogs.SelectionStart = txtLogs.Text.Length;
            txtLogs.ScrollToCaret();

            AppLogger.OnLogAdded += HandleLogAdded;
        }

        private void InitializeComponent(Icon? appIcon)
        {
            this.Text = "Rapid Downloader - Application Logs";
            this.Size = new Size(800, 500);
            this.MinimumSize = new Size(600, 350);
            this.StartPosition = FormStartPosition.CenterParent;
            if (appIcon != null) this.Icon = appIcon;

            var panelBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                BackColor = SystemColors.Control
            };

            btnCopy = new Button
            {
                Text = "Copy All",
                Size = new Size(90, 30),
                Location = new Point(12, 9)
            };
            btnCopy.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(txtLogs.Text))
                {
                    Clipboard.SetText(txtLogs.Text);
                    MessageBox.Show("Logs copied to clipboard.", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            btnClear = new Button
            {
                Text = "Clear Logs",
                Size = new Size(90, 30),
                Location = new Point(110, 9)
            };
            btnClear.Click += (s, e) =>
            {
                AppLogger.Clear();
                txtLogs.Clear();
            };

            btnClose = new Button
            {
                Text = "Close",
                Size = new Size(85, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(panelBottom.Width - 97, 9)
            };
            btnClose.Click += (s, e) => this.Close();

            panelBottom.Controls.AddRange(new Control[] { btnCopy, btnClear, btnClose });

            txtLogs = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Consolas", 9.5f, FontStyle.Regular)
            };

            this.Controls.Add(txtLogs);
            this.Controls.Add(panelBottom);
        }

        private void HandleLogAdded(string entry)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;

            this.BeginInvoke(new Action(() =>
            {
                txtLogs.AppendText(entry + Environment.NewLine);
            }));
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            AppLogger.OnLogAdded -= HandleLogAdded;
            base.OnFormClosed(e);
        }
    }
}