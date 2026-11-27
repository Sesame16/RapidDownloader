using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Rapid_Downloader
{
    public class SegmentsTabControl : UserControl, IOptionsTab
    {
        // Max Connections Controls
        private Label lblDefaultConn = null!;
        private NumericUpDown numDefaultConnections = null!;
        private CheckBox chkAllowMore32 = null!;
        private ListView lvServerExceptions = null!;
        private Button btnNewException = null!;
        private Button btnEditException = null!;
        private Button btnDeleteException = null!;

        // Download Limits Controls
        private CheckBox chkEnableLimits = null!;
        private Label lblNoMoreThan = null!;
        private NumericUpDown numLimitMBytes = null!;
        private Label lblMBytes = null!;
        private Label lblEvery = null!;
        private NumericUpDown numLimitHours = null!;
        private Label lblHours = null!;
        private CheckBox chkWarnBeforeStop = null!;

        // DNS Controls
        private ComboBox cboDns = null!;
        private Button btnDnsSettings = null!;
        private Label lblDnsDetails = null!;

        // Working state
        private List<ServerConnectionException> _workingExceptions = new();
        private CustomDnsConfig _workingCustomDns = new();

        public SegmentsTabControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.White;
            this.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            // 1. "Max. connections number" GroupBox
            var grpConnections = new GroupBox
            {
                Text = "Max. connections number",
                Location = new Point(16, 12),
                Size = new Size(640, 248),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            // FlowLayoutPanel prevents label & NumericUpDown overlap across all Windows DPI scales
            var flowDefaultConn = new FlowLayoutPanel
            {
                Location = new Point(16, 24),
                Size = new Size(608, 30),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            lblDefaultConn = new Label
            {
                Text = "Default max. conn. number:",
                AutoSize = true,
                Margin = new Padding(0, 5, 8, 0)
            };

            numDefaultConnections = new NumericUpDown
            {
                Size = new Size(85, 25),
                Minimum = 1,
                Maximum = 32,
                Value = 8,
                Margin = new Padding(0, 1, 0, 0)
            };

            flowDefaultConn.Controls.AddRange(new Control[] { lblDefaultConn, numDefaultConnections });

            chkAllowMore32 = new CheckBox
            {
                Text = "Allow more than 32 connections (up to 64 parallel streams)",
                Location = new Point(18, 58),
                AutoSize = true
            };
            chkAllowMore32.CheckedChanged += (s, e) =>
            {
                int newMax = chkAllowMore32.Checked ? 64 : 32;
                if (numDefaultConnections.Value > newMax)
                    numDefaultConnections.Value = newMax;
                numDefaultConnections.Maximum = newMax;
            };

            var divider = new Label
            {
                BorderStyle = BorderStyle.Fixed3D,
                Location = new Point(18, 88),
                Size = new Size(604, 2)
            };

            var lblExceptions = new Label
            {
                Text = "Exceptions (custom connection limits per server):",
                Location = new Point(18, 98),
                AutoSize = true
            };

            lvServerExceptions = new ListView
            {
                Location = new Point(18, 122),
                Size = new Size(490, 110),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };
            lvServerExceptions.Columns.Add("Server", 385);
            lvServerExceptions.Columns.Add("Number", 95);
            lvServerExceptions.DoubleClick += BtnEditException_Click;
            lvServerExceptions.SelectedIndexChanged += (s, e) => UpdateExceptionButtonsState();

            btnNewException = new Button
            {
                Text = "New",
                Location = new Point(520, 122),
                Size = new Size(102, 28)
            };
            btnNewException.Click += BtnNewException_Click;

            btnEditException = new Button
            {
                Text = "Edit...",
                Location = new Point(520, 158),
                Size = new Size(102, 28),
                Enabled = false
            };
            btnEditException.Click += BtnEditException_Click;

            btnDeleteException = new Button
            {
                Text = "Delete",
                Location = new Point(520, 194),
                Size = new Size(102, 28),
                Enabled = false
            };
            btnDeleteException.Click += BtnDeleteException_Click;

            grpConnections.Controls.AddRange(new Control[]
            {
                flowDefaultConn, chkAllowMore32,
                divider, lblExceptions, lvServerExceptions,
                btnNewException, btnEditException, btnDeleteException
            });

            // 2. "Download limits" GroupBox
            var grpLimits = new GroupBox
            {
                Text = "Download limits",
                Location = new Point(16, 270),
                Size = new Size(640, 148),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            chkEnableLimits = new CheckBox
            {
                Text = "Enable download quota limits",
                Location = new Point(18, 24),
                AutoSize = true
            };
            chkEnableLimits.CheckedChanged += (s, e) => UpdateLimitsEnabledState();

            var flowLimitSize = new FlowLayoutPanel
            {
                Location = new Point(38, 54),
                Size = new Size(580, 30),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            lblNoMoreThan = new Label
            {
                Text = "Download no more than",
                AutoSize = true,
                Margin = new Padding(0, 5, 8, 0)
            };

            numLimitMBytes = new NumericUpDown
            {
                Size = new Size(95, 25),
                Minimum = 1,
                Maximum = 999999,
                Value = 200,
                Margin = new Padding(0, 1, 8, 0)
            };

            lblMBytes = new Label
            {
                Text = "MBytes",
                AutoSize = true,
                Margin = new Padding(0, 5, 0, 0)
            };

            flowLimitSize.Controls.AddRange(new Control[] { lblNoMoreThan, numLimitMBytes, lblMBytes });

            var flowLimitHours = new FlowLayoutPanel
            {
                Location = new Point(38, 86),
                Size = new Size(580, 30),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            lblEvery = new Label
            {
                Text = "every",
                AutoSize = true,
                Margin = new Padding(0, 5, 8, 0)
            };

            numLimitHours = new NumericUpDown
            {
                Size = new Size(85, 25),
                Minimum = 1,
                Maximum = 720,
                Value = 5,
                Margin = new Padding(0, 1, 8, 0)
            };

            lblHours = new Label
            {
                Text = "hours",
                AutoSize = true,
                Margin = new Padding(0, 5, 0, 0)
            };

            flowLimitHours.Controls.AddRange(new Control[] { lblEvery, numLimitHours, lblHours });

            chkWarnBeforeStop = new CheckBox
            {
                Text = "Show warning before stopping downloads",
                Location = new Point(40, 118),
                AutoSize = true,
                Checked = true
            };

            grpLimits.Controls.AddRange(new Control[]
            {
                chkEnableLimits,
                flowLimitSize,
                flowLimitHours,
                chkWarnBeforeStop
            });

            // 3. DNS Selector Row
            var lblDns = new Label
            {
                Text = "DNS:",
                Location = new Point(20, 432),
                AutoSize = true
            };

            cboDns = new ComboBox
            {
                Location = new Point(64, 428),
                Size = new Size(476, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (var preset in DnsCatalog.Presets)
            {
                cboDns.Items.Add(preset.DisplayName);
            }
            cboDns.SelectedIndexChanged += (s, e) => UpdateDnsStatusDisplay();

            btnDnsSettings = new Button
            {
                Text = "Settings...",
                Location = new Point(550, 426),
                Size = new Size(106, 28)
            };
            btnDnsSettings.Click += BtnDnsSettings_Click;

            lblDnsDetails = new Label
            {
                Text = "Uses Windows default network adapter DNS configuration.",
                Location = new Point(64, 458),
                Size = new Size(592, 20),
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular)
            };

            this.Controls.AddRange(new Control[]
            {
                grpConnections,
                grpLimits,
                lblDns,
                cboDns,
                btnDnsSettings,
                lblDnsDetails
            });
        }

        public void LoadSettings(AppSettings settings)
        {
            chkAllowMore32.Checked = settings.AllowMoreThan32Connections;
            int maxConn = chkAllowMore32.Checked ? 64 : 32;
            numDefaultConnections.Maximum = maxConn;
            numDefaultConnections.Value = Math.Clamp(settings.DefaultMaxConnections, 1, maxConn);

            _workingExceptions = settings.ServerExceptions
                .Select(e => new ServerConnectionException(e.ServerPattern, e.Connections))
                .ToList();
            RefreshServerExceptionsList();

            chkEnableLimits.Checked = settings.EnableDownloadLimits;
            numLimitMBytes.Value = Math.Clamp(settings.DownloadLimitMBytes, 1, 999999);
            numLimitHours.Value = Math.Clamp(settings.DownloadLimitHours, 1, 720);
            chkWarnBeforeStop.Checked = settings.WarnBeforeStoppingLimit;
            UpdateLimitsEnabledState();

            _workingCustomDns = new CustomDnsConfig
            {
                Name = settings.CustomDns.Name,
                PrimaryIp = settings.CustomDns.PrimaryIp,
                SecondaryIp = settings.CustomDns.SecondaryIp,
                DohUrl = settings.CustomDns.DohUrl
            };

            if (cboDns.Items.Contains(settings.SelectedDnsProvider))
                cboDns.SelectedItem = settings.SelectedDnsProvider;
            else
                cboDns.SelectedIndex = 0;

            UpdateDnsStatusDisplay();
        }

        public bool ValidateSettings(out string errorMessage)
        {
            if (numDefaultConnections.Value < 1)
            {
                errorMessage = "Default maximum connections must be at least 1.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public void ApplySettings(AppSettings settings)
        {
            settings.AllowMoreThan32Connections = chkAllowMore32.Checked;
            settings.DefaultMaxConnections = (int)numDefaultConnections.Value;
            settings.ServerExceptions = _workingExceptions
                .Select(e => new ServerConnectionException(e.ServerPattern, e.Connections))
                .ToList();

            settings.EnableDownloadLimits = chkEnableLimits.Checked;
            settings.DownloadLimitMBytes = (int)numLimitMBytes.Value;
            settings.DownloadLimitHours = (int)numLimitHours.Value;
            settings.WarnBeforeStoppingLimit = chkWarnBeforeStop.Checked;

            settings.SelectedDnsProvider = cboDns.SelectedItem?.ToString() ?? DnsCatalog.SystemDnsName;
            settings.CustomDns = new CustomDnsConfig
            {
                Name = _workingCustomDns.Name,
                PrimaryIp = _workingCustomDns.PrimaryIp,
                SecondaryIp = _workingCustomDns.SecondaryIp,
                DohUrl = _workingCustomDns.DohUrl
            };
        }

        private void RefreshServerExceptionsList()
        {
            lvServerExceptions.Items.Clear();
            foreach (var ex in _workingExceptions)
            {
                var lvi = new ListViewItem(ex.ServerPattern);
                lvi.SubItems.Add(ex.Connections.ToString());
                lvi.Tag = ex;
                lvServerExceptions.Items.Add(lvi);
            }
            UpdateExceptionButtonsState();
        }

        private void UpdateExceptionButtonsState()
        {
            bool hasSelection = lvServerExceptions.SelectedItems.Count > 0;
            btnEditException.Enabled = hasSelection;
            btnDeleteException.Enabled = hasSelection;
        }

        private static string NormalizeServerPattern(string input)
        {
            string trimmed = input.Trim();
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? parsedUri) && !string.IsNullOrWhiteSpace(parsedUri.Host))
            {
                return parsedUri.Host.ToLowerInvariant();
            }

            int slashIdx = trimmed.IndexOf('/');
            if (slashIdx > 0)
            {
                trimmed = trimmed.Substring(0, slashIdx);
            }

            return trimmed.ToLowerInvariant();
        }

        private void BtnNewException_Click(object? sender, EventArgs e)
        {
            int maxConn = chkAllowMore32.Checked ? 64 : 32;
            Icon? parentIcon = this.FindForm()?.Icon;
            using var dlg = new ServerExceptionDialog("", 4, maxConn, false, parentIcon);
            if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                string normalized = NormalizeServerPattern(dlg.ServerPattern);
                var existing = _workingExceptions.FirstOrDefault(x => x.ServerPattern.Equals(normalized, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Connections = dlg.Connections;
                }
                else
                {
                    _workingExceptions.Add(new ServerConnectionException(normalized, dlg.Connections));
                }
                RefreshServerExceptionsList();
            }
        }

        private void BtnEditException_Click(object? sender, EventArgs e)
        {
            if (lvServerExceptions.SelectedItems.Count == 0) return;
            if (lvServerExceptions.SelectedItems[0].Tag is not ServerConnectionException selectedEx) return;

            int maxConn = chkAllowMore32.Checked ? 64 : 32;
            Icon? parentIcon = this.FindForm()?.Icon;
            using var dlg = new ServerExceptionDialog(selectedEx.ServerPattern, selectedEx.Connections, maxConn, true, parentIcon);
            if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                selectedEx.ServerPattern = NormalizeServerPattern(dlg.ServerPattern);
                selectedEx.Connections = dlg.Connections;
                RefreshServerExceptionsList();
            }
        }

        private void BtnDeleteException_Click(object? sender, EventArgs e)
        {
            if (lvServerExceptions.SelectedItems.Count == 0) return;
            if (lvServerExceptions.SelectedItems[0].Tag is not ServerConnectionException selectedEx) return;

            _workingExceptions.Remove(selectedEx);
            RefreshServerExceptionsList();
        }

        private void UpdateLimitsEnabledState()
        {
            bool enabled = chkEnableLimits.Checked;
            lblNoMoreThan.Enabled = enabled;
            numLimitMBytes.Enabled = enabled;
            lblMBytes.Enabled = enabled;
            lblEvery.Enabled = enabled;
            numLimitHours.Enabled = enabled;
            lblHours.Enabled = enabled;
            chkWarnBeforeStop.Enabled = enabled;
        }

        private void UpdateDnsStatusDisplay()
        {
            string selected = cboDns.SelectedItem?.ToString() ?? DnsCatalog.SystemDnsName;

            if (selected == DnsCatalog.SystemDnsName)
            {
                btnDnsSettings.Enabled = false;
                lblDnsDetails.Text = "Uses Windows default network adapter DNS configuration.";
            }
            else if (selected == DnsCatalog.CustomDnsName)
            {
                btnDnsSettings.Enabled = true;
                string ipInfo = string.IsNullOrWhiteSpace(_workingCustomDns.PrimaryIp) ? "DoH only" : _workingCustomDns.PrimaryIp;
                string dohInfo = string.IsNullOrWhiteSpace(_workingCustomDns.DohUrl) ? "No DoH URL set" : _workingCustomDns.DohUrl;
                lblDnsDetails.Text = $"Custom ({_workingCustomDns.Name}): {ipInfo} | {dohInfo}";
            }
            else
            {
                var preset = DnsCatalog.FindPreset(selected);
                btnDnsSettings.Enabled = true;
                lblDnsDetails.Text = preset != null
                    ? $"DoH Endpoint: {preset.DohUrl}"
                    : string.Empty;
            }
        }

        private void BtnDnsSettings_Click(object? sender, EventArgs e)
        {
            string selected = cboDns.SelectedItem?.ToString() ?? DnsCatalog.SystemDnsName;

            if (selected != DnsCatalog.CustomDnsName)
            {
                var preset = DnsCatalog.FindPreset(selected);
                if (preset != null)
                {
                    _workingCustomDns = new CustomDnsConfig
                    {
                        Name = preset.DisplayName.Split('(')[0].Trim(),
                        PrimaryIp = preset.PrimaryIp,
                        SecondaryIp = preset.SecondaryIp,
                        DohUrl = preset.DohUrl
                    };
                }
            }

            Icon? parentIcon = this.FindForm()?.Icon;
            using var dlg = new CustomDnsDialog(_workingCustomDns, parentIcon);
            if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                _workingCustomDns = dlg.ResultConfig;
                cboDns.SelectedItem = DnsCatalog.CustomDnsName;
                UpdateDnsStatusDisplay();
            }
        }
    }
}