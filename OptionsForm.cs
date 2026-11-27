using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Rapid_Downloader
{
    public class OptionsForm : Form
    {
        private TabControl tabControl = null!;
        private readonly List<IOptionsTab> _tabs = new();

        private SaveFileTabControl saveFileTab = null!;
        private SegmentsTabControl segmentsTab = null!;
        private ProxyTabControl proxyTab = null!;
        private AdvancedTabControl advancedTab = null!;
        private BrowserIntegrationTabControl browserIntegrationTab = null!;

        private Button btnResetDefaults = null!;
        private Button btnOk = null!;
        private Button btnCancel = null!;
        private Button btnApply = null!;

        public OptionsForm(Icon? appIcon)
        {
            InitializeComponent(appIcon);
            LoadAllTabs(AppSettings.Current);
        }

        private void InitializeComponent(Icon? appIcon)
        {
            this.Text = "Configuration Options";
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(684, 585);
            this.MinimumSize = new Size(700, 624);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            if (appIcon != null) this.Icon = appIcon;

            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(12, 6),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            saveFileTab = new SaveFileTabControl();
            segmentsTab = new SegmentsTabControl();
            proxyTab = new ProxyTabControl();
            advancedTab = new AdvancedTabControl();
            browserIntegrationTab = new BrowserIntegrationTabControl();

            AddTabPage("Save File", saveFileTab);
            AddTabPage("Segments", segmentsTab);
            AddTabPage("Proxy", proxyTab);
            AddTabPage("Advanced", advancedTab);
            AddTabPage("Browser Integration", browserIntegrationTab);

            var panelBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                BackColor = SystemColors.Control
            };

            var topBorder = new Label
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Color.FromArgb(215, 215, 215)
            };

            btnResetDefaults = new Button
            {
                Text = "Reset Defaults",
                Size = new Size(125, 30),
                Location = new Point(16, 12),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };
            btnResetDefaults.Click += BtnResetDefaults_Click;

            btnOk = new Button
            {
                Text = "OK",
                Size = new Size(88, 30),
                Location = new Point(392, 12),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };
            btnOk.Click += (s, e) =>
            {
                if (TryApplyAllTabs())
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(88, 30),
                Location = new Point(488, 12),
                DialogResult = DialogResult.Cancel,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };
            btnCancel.Click += (s, e) => this.Close();

            btnApply = new Button
            {
                Text = "Apply",
                Size = new Size(88, 30),
                Location = new Point(584, 12),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };
            btnApply.Click += (s, e) => TryApplyAllTabs();

            panelBottom.Controls.AddRange(new Control[]
            {
                topBorder,
                btnResetDefaults,
                btnOk,
                btnCancel,
                btnApply
            });

            // Add Fill control before Bottom panel so Dock=Fill reserves the bottom bar cleanly
            this.Controls.Add(tabControl);
            this.Controls.Add(panelBottom);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void AddTabPage(string title, Control tabContent)
        {
            var page = new TabPage(title)
            {
                BackColor = Color.White,
                Padding = Padding.Empty
            };
            page.Controls.Add(tabContent);
            tabControl.TabPages.Add(page);

            if (tabContent is IOptionsTab optionsTab)
            {
                _tabs.Add(optionsTab);
            }
        }

        private void LoadAllTabs(AppSettings source)
        {
            foreach (var tab in _tabs)
            {
                tab.LoadSettings(source);
            }
        }

        private bool TryApplyAllTabs()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (!_tabs[i].ValidateSettings(out string errorMessage))
                {
                    tabControl.SelectedIndex = i;
                    MessageBox.Show(errorMessage, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
            }

            var settings = AppSettings.Current;
            foreach (var tab in _tabs)
            {
                tab.ApplySettings(settings);
            }

            settings.Save();
            AppLogger.Info($"[Options] Applied settings: MaxConn={settings.DefaultMaxConnections}, DNS='{settings.SelectedDnsProvider}', Bridge={settings.EnableBrowserIntegration} (Port {settings.BridgePort})");
            return true;
        }

        private void BtnResetDefaults_Click(object? sender, EventArgs e)
        {
            var confirm = MessageBox.Show(
                "Restore all options to their default values?\n\nClick OK or Apply afterward to save the reset configuration.",
                "Reset Defaults",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            var defaults = new AppSettings
            {
                // Preserve existing bridge pairing token so already-installed browser extensions remain paired
                BridgeAuthToken = AppSettings.Current.BridgeAuthToken
            };
            defaults.InitializeDefaultCategories();
            LoadAllTabs(defaults);
        }
    }
}