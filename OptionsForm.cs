using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Rapid_Downloader
{
    public class OptionsForm : Form
    {
        private TabControl tabControl = null!;
        private TabPage tabSaveFile = null!;
        private TabPage tabSegments = null!;
        private TabPage tabProxy = null!;
        private TabPage tabAdvanced = null!;

        // Save File Tab Controls
        private ComboBox cboCategory = null!;
        private Button btnNewCategory = null!;
        private Button btnEditCategory = null!;
        private Button btnDeleteCategory = null!;
        private Label lblAutoPutIn = null!;
        private TextBox txtFileTypes = null!;
        private CheckBox chkSetCreationDate = null!;
        private TextBox txtTempDir = null!;
        private Button btnBrowseTemp = null!;

        // Segments Tab Controls
        private NumericUpDown numDefaultConnections = null!;
        private CheckBox chkAllowMore32 = null!;
        private ListView lvServerExceptions = null!;
        private Button btnNewException = null!;
        private Button btnEditException = null!;
        private Button btnDeleteException = null!;

        private CheckBox chkEnableLimits = null!;
        private Label lblNoMoreThan = null!;
        private NumericUpDown numLimitMBytes = null!;
        private Label lblMBytes = null!;
        private Label lblEvery = null!;
        private NumericUpDown numLimitHours = null!;
        private Label lblHours = null!;
        private CheckBox chkWarnBeforeStop = null!;

        private ComboBox cboDns = null!;
        private Button btnDnsSettings = null!;
        private Label lblDnsDetails = null!;

        // Bottom Dialog Buttons
        private Button btnOk = null!;
        private Button btnCancel = null!;
        private Button btnApply = null!;

        // Working state
        private List<CategoryConfig> _workingCategories = new();
        private List<ServerConnectionException> _workingExceptions = new();
        private CustomDnsConfig _workingCustomDns = new();

        public OptionsForm(Icon? appIcon)
        {
            InitializeComponent(appIcon);
            LoadSettingsToUI();
        }

        private void InitializeComponent(Icon? appIcon)
        {
            this.Text = "Configuration Options";
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Size = new Size(690, 635);
            this.MinimumSize = new Size(690, 635);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            if (appIcon != null) this.Icon = appIcon;

            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(14, 6),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            tabSaveFile = new TabPage("Save File");
            tabSegments = new TabPage("Segments");
            tabProxy = new TabPage("Proxy");
            tabAdvanced = new TabPage("Advanced");

            tabControl.TabPages.AddRange(new[] { tabSaveFile, tabSegments, tabProxy, tabAdvanced });

            BuildSaveFileTab();
            BuildSegmentsTab();
            BuildPlaceholderTab(tabProxy, "Proxy & Network Routing", "Configure HTTP, HTTPS, SOCKS5 proxies, and routing authentication.");
            BuildPlaceholderTab(tabAdvanced, "Advanced Application Settings", "Configure custom TLS parameters, browser integration hooks, and protocol handlers.");

            var panelBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                BackColor = SystemColors.Control
            };

            btnOk = new Button
            {
                Text = "OK",
                Size = new Size(88, 30),
                Location = new Point(386, 12),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };
            btnOk.Click += (s, e) =>
            {
                ApplySettings();
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(88, 30),
                Location = new Point(482, 12),
                DialogResult = DialogResult.Cancel,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };
            btnCancel.Click += (s, e) => this.Close();

            btnApply = new Button
            {
                Text = "Apply",
                Size = new Size(88, 30),
                Location = new Point(578, 12),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };
            btnApply.Click += (s, e) => ApplySettings();

            panelBottom.Controls.AddRange(new Control[] { btnOk, btnCancel, btnApply });

            // Add Fill control first, Bottom panel second so Dock=Fill reserves the bottom bar cleanly
            this.Controls.Add(tabControl);
            this.Controls.Add(panelBottom);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void BuildSaveFileTab()
        {
            tabSaveFile.BackColor = Color.White;

            var bannerPanel = new Panel
            {
                Location = new Point(16, 12),
                Size = new Size(630, 36)
            };

            var picFolder = new PictureBox
            {
                Image = SystemIcons.Application.ToBitmap(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(4, 3),
                Size = new Size(28, 28)
            };

            var lblHeader = new Label
            {
                Text = "Categories, file types, and storage",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Location = new Point(40, 6),
                AutoSize = true,
                ForeColor = Color.FromArgb(40, 40, 40)
            };

            bannerPanel.Controls.AddRange(new Control[] { picFolder, lblHeader });

            var grpSaveTo = new GroupBox
            {
                Text = "Save To...",
                Location = new Point(16, 54),
                Size = new Size(630, 205),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            var lblCategory = new Label
            {
                Text = "Category",
                Location = new Point(16, 26),
                AutoSize = true
            };

            cboCategory = new ComboBox
            {
                Location = new Point(16, 48),
                Size = new Size(480, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboCategory.SelectedIndexChanged += CboCategory_SelectedIndexChanged;

            btnNewCategory = new Button
            {
                Text = "New",
                Location = new Point(510, 24),
                Size = new Size(102, 28)
            };
            btnNewCategory.Click += BtnNewCategory_Click;

            btnEditCategory = new Button
            {
                Text = "Edit...",
                Location = new Point(510, 58),
                Size = new Size(102, 28)
            };
            btnEditCategory.Click += BtnEditCategory_Click;

            btnDeleteCategory = new Button
            {
                Text = "Delete",
                Location = new Point(510, 92),
                Size = new Size(102, 28)
            };
            btnDeleteCategory.Click += BtnDeleteCategory_Click;

            lblAutoPutIn = new Label
            {
                Text = "Automatically put in category the following file types:",
                Location = new Point(16, 94),
                Size = new Size(480, 20)
            };

            txtFileTypes = new TextBox
            {
                Location = new Point(16, 118),
                Size = new Size(480, 70),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = Color.FromArgb(248, 249, 250)
            };

            grpSaveTo.Controls.AddRange(new Control[]
            {
                lblCategory, cboCategory,
                btnNewCategory, btnEditCategory, btnDeleteCategory,
                lblAutoPutIn, txtFileTypes
            });

            chkSetCreationDate = new CheckBox
            {
                Text = "Set file creation date as provided by the server",
                Location = new Point(20, 270),
                Size = new Size(500, 24),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            var grpTemp = new GroupBox
            {
                Text = "Temporary directory",
                Location = new Point(16, 304),
                Size = new Size(630, 148),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            txtTempDir = new TextBox
            {
                Location = new Point(16, 30),
                Size = new Size(485, 23)
            };

            btnBrowseTemp = new Button
            {
                Text = "Browse",
                Location = new Point(512, 28),
                Size = new Size(100, 27)
            };
            btnBrowseTemp.Click += BtnBrowseTemp_Click;

            var lblTempDesc = new Label
            {
                Text = "Temporary directory used to store partial file segments during parallel downloads.\n" +
                       "Selecting a different physical drive from your destination folder can improve " +
                       "disk throughput when assembling large downloaded files.",
                Location = new Point(16, 66),
                Size = new Size(595, 65),
                ForeColor = Color.FromArgb(90, 90, 90)
            };

            grpTemp.Controls.AddRange(new Control[] { txtTempDir, btnBrowseTemp, lblTempDesc });

            tabSaveFile.Controls.AddRange(new Control[] { bannerPanel, grpSaveTo, chkSetCreationDate, grpTemp });
        }

        private void BuildSegmentsTab()
        {
            tabSegments.BackColor = Color.White;

            // 1. Max. Connections Number GroupBox
            var grpConnections = new GroupBox
            {
                Text = "Max. connections number",
                Location = new Point(16, 14),
                Size = new Size(630, 242),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            var lblDefaultConn = new Label
            {
                Text = "Default max. conn. number:",
                Location = new Point(18, 29),
                AutoSize = true
            };

            numDefaultConnections = new NumericUpDown
            {
                Location = new Point(205, 26),
                Size = new Size(85, 23),
                Minimum = 1,
                Maximum = 32,
                Value = 8
            };

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
                Size = new Size(594, 2)
            };

            var lblExceptions = new Label
            {
                Text = "Exceptions (custom connection limits per server):",
                Location = new Point(18, 98),
                AutoSize = true
            };

            lvServerExceptions = new ListView
            {
                Location = new Point(18, 120),
                Size = new Size(482, 106),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };
            lvServerExceptions.Columns.Add("Server", 360);
            lvServerExceptions.Columns.Add("Number", 95);
            lvServerExceptions.DoubleClick += BtnEditException_Click;

            btnNewException = new Button
            {
                Text = "New",
                Location = new Point(512, 120),
                Size = new Size(100, 28)
            };
            btnNewException.Click += BtnNewException_Click;

            btnEditException = new Button
            {
                Text = "Edit...",
                Location = new Point(512, 156),
                Size = new Size(100, 28)
            };
            btnEditException.Click += BtnEditException_Click;

            btnDeleteException = new Button
            {
                Text = "Delete",
                Location = new Point(512, 192),
                Size = new Size(100, 28)
            };
            btnDeleteException.Click += BtnDeleteException_Click;

            grpConnections.Controls.AddRange(new Control[]
            {
                lblDefaultConn, numDefaultConnections, chkAllowMore32,
                divider, lblExceptions, lvServerExceptions,
                btnNewException, btnEditException, btnDeleteException
            });

            // 2. Download Limits GroupBox
            var grpLimits = new GroupBox
            {
                Text = "Download limits",
                Location = new Point(16, 266),
                Size = new Size(630, 142),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            chkEnableLimits = new CheckBox
            {
                Text = "Enable download quota limits",
                Location = new Point(18, 24),
                AutoSize = true
            };
            chkEnableLimits.CheckedChanged += (s, e) => UpdateLimitsEnabledState();

            lblNoMoreThan = new Label
            {
                Text = "Download no more than",
                Location = new Point(42, 55),
                AutoSize = true
            };

            numLimitMBytes = new NumericUpDown
            {
                Location = new Point(195, 52),
                Size = new Size(90, 23),
                Minimum = 1,
                Maximum = 999999,
                Value = 200
            };

            lblMBytes = new Label
            {
                Text = "MBytes",
                Location = new Point(292, 55),
                AutoSize = true
            };

            lblEvery = new Label
            {
                Text = "every",
                Location = new Point(152, 85),
                AutoSize = true
            };

            numLimitHours = new NumericUpDown
            {
                Location = new Point(195, 82),
                Size = new Size(90, 23),
                Minimum = 1,
                Maximum = 720,
                Value = 5
            };

            lblHours = new Label
            {
                Text = "hours",
                Location = new Point(292, 85),
                AutoSize = true
            };

            chkWarnBeforeStop = new CheckBox
            {
                Text = "Show warning before stopping downloads",
                Location = new Point(42, 112),
                AutoSize = true,
                Checked = true
            };

            grpLimits.Controls.AddRange(new Control[]
            {
                chkEnableLimits,
                lblNoMoreThan, numLimitMBytes, lblMBytes,
                lblEvery, numLimitHours, lblHours,
                chkWarnBeforeStop
            });

            // 3. DNS Selector Row
            var lblDns = new Label
            {
                Text = "DNS:",
                Location = new Point(20, 424),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            cboDns = new ComboBox
            {
                Location = new Point(65, 420),
                Size = new Size(465, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (var preset in DnsCatalog.Presets)
            {
                cboDns.Items.Add(preset.DisplayName);
            }
            cboDns.SelectedIndexChanged += CboDns_SelectedIndexChanged;

            btnDnsSettings = new Button
            {
                Text = "Settings...",
                Location = new Point(540, 418),
                Size = new Size(106, 28)
            };
            btnDnsSettings.Click += BtnDnsSettings_Click;

            lblDnsDetails = new Label
            {
                Text = "Uses Windows default network adapter DNS configuration.",
                Location = new Point(65, 450),
                Size = new Size(580, 20),
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular)
            };

            tabSegments.Controls.AddRange(new Control[]
            {
                grpConnections, grpLimits,
                lblDns, cboDns, btnDnsSettings, lblDnsDetails
            });
        }

        private void BuildPlaceholderTab(TabPage page, string title, string description)
        {
            page.BackColor = Color.White;

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Location = new Point(25, 30),
                AutoSize = true,
                ForeColor = Color.FromArgb(40, 40, 40)
            };

            var lblDesc = new Label
            {
                Text = description + "\n\n(This module will be activated in the next development phase.)",
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Location = new Point(25, 65),
                Size = new Size(560, 80),
                ForeColor = Color.Gray
            };

            page.Controls.AddRange(new Control[] { lblTitle, lblDesc });
        }

        private void LoadSettingsToUI()
        {
            var settings = AppSettings.Current;

            // Save File Tab
            _workingCategories = settings.Categories
                .Select(c => new CategoryConfig(c.Name, c.Extensions, c.IsDefault))
                .ToList();
            PopulateCategoriesCombo();

            txtTempDir.Text = settings.TempDirectory;
            chkSetCreationDate.Checked = settings.SetFileCreationDateFromServer;

            // Segments Tab
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

        private void PopulateCategoriesCombo(string? selectName = null)
        {
            cboCategory.Items.Clear();
            foreach (var cat in _workingCategories)
            {
                cboCategory.Items.Add(cat.Name);
            }

            if (!string.IsNullOrEmpty(selectName) && cboCategory.Items.Contains(selectName))
                cboCategory.SelectedItem = selectName;
            else if (cboCategory.Items.Count > 0)
                cboCategory.SelectedIndex = 0;
        }

        private void CboCategory_SelectedIndexChanged(object? sender, EventArgs e)
        {
            string selected = cboCategory.SelectedItem?.ToString() ?? string.Empty;
            var cat = _workingCategories.FirstOrDefault(c => c.Name == selected);

            if (cat != null)
            {
                lblAutoPutIn.Text = $"Automatically put in '{cat.Name}' category the following file types:";
                txtFileTypes.Text = string.IsNullOrWhiteSpace(cat.Extensions)
                    ? "The file types that are not listed in any other category"
                    : cat.Extensions;

                btnDeleteCategory.Enabled = !cat.IsDefault;
            }
        }

        private void BtnNewCategory_Click(object? sender, EventArgs e)
        {
            using var dlg = new CategoryEditDialog("", "", false, this.Icon);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                if (_workingCategories.Any(c => c.Name.Equals(dlg.CategoryName, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show($"Category '{dlg.CategoryName}' already exists.", "Duplicate Category", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var newCat = new CategoryConfig(dlg.CategoryName, dlg.Extensions, false);
                _workingCategories.Add(newCat);
                PopulateCategoriesCombo(newCat.Name);
            }
        }

        private void BtnEditCategory_Click(object? sender, EventArgs e)
        {
            string selected = cboCategory.SelectedItem?.ToString() ?? string.Empty;
            var cat = _workingCategories.FirstOrDefault(c => c.Name == selected);
            if (cat == null) return;

            using var dlg = new CategoryEditDialog(cat.Name, cat.Extensions, true, this.Icon);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                cat.Name = dlg.CategoryName;
                cat.Extensions = dlg.Extensions;
                PopulateCategoriesCombo(cat.Name);
            }
        }

        private void BtnDeleteCategory_Click(object? sender, EventArgs e)
        {
            string selected = cboCategory.SelectedItem?.ToString() ?? string.Empty;
            var cat = _workingCategories.FirstOrDefault(c => c.Name == selected);
            if (cat == null || cat.IsDefault) return;

            var confirm = MessageBox.Show($"Remove category '{cat.Name}'?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.Yes)
            {
                _workingCategories.Remove(cat);
                PopulateCategoriesCombo();
            }
        }

        private void BtnBrowseTemp_Click(object? sender, EventArgs e)
        {
            using var fbd = new FolderBrowserDialog();
            fbd.Description = "Select Temporary Storage Directory";
            if (Directory.Exists(txtTempDir.Text))
            {
                fbd.SelectedPath = txtTempDir.Text;
            }

            if (fbd.ShowDialog(this) == DialogResult.OK)
            {
                txtTempDir.Text = fbd.SelectedPath;
            }
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
        }

        private void BtnNewException_Click(object? sender, EventArgs e)
        {
            int maxConn = chkAllowMore32.Checked ? 64 : 32;
            using var dlg = new ServerExceptionDialog("", 4, maxConn, false, this.Icon);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                var existing = _workingExceptions.FirstOrDefault(x => x.ServerPattern.Equals(dlg.ServerPattern, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Connections = dlg.Connections;
                }
                else
                {
                    _workingExceptions.Add(new ServerConnectionException(dlg.ServerPattern, dlg.Connections));
                }
                RefreshServerExceptionsList();
            }
        }

        private void BtnEditException_Click(object? sender, EventArgs e)
        {
            if (lvServerExceptions.SelectedItems.Count == 0) return;
            if (lvServerExceptions.SelectedItems[0].Tag is not ServerConnectionException selectedEx) return;

            int maxConn = chkAllowMore32.Checked ? 64 : 32;
            using var dlg = new ServerExceptionDialog(selectedEx.ServerPattern, selectedEx.Connections, maxConn, true, this.Icon);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                selectedEx.ServerPattern = dlg.ServerPattern;
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

        private void CboDns_SelectedIndexChanged(object? sender, EventArgs e)
        {
            UpdateDnsStatusDisplay();
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
                // Allow Settings... on NextDNS or any preset so users can customize/append profile IDs
                btnDnsSettings.Enabled = true;
                lblDnsDetails.Text = preset != null
                    ? $"DoH Endpoint: {preset.DohUrl}"
                    : string.Empty;
            }
        }

        private void BtnDnsSettings_Click(object? sender, EventArgs e)
        {
            string selected = cboDns.SelectedItem?.ToString() ?? DnsCatalog.SystemDnsName;

            // If user clicked Settings on a preset (e.g., NextDNS), seed CustomDnsConfig from that preset
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

            using var dlg = new CustomDnsDialog(_workingCustomDns, this.Icon);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _workingCustomDns = dlg.ResultConfig;
                cboDns.SelectedItem = DnsCatalog.CustomDnsName;
                UpdateDnsStatusDisplay();
            }
        }

        private void ApplySettings()
        {
            var settings = AppSettings.Current;

            // Save File Settings
            settings.Categories = _workingCategories
                .Select(c => new CategoryConfig(c.Name, c.Extensions, c.IsDefault))
                .ToList();
            settings.TempDirectory = string.IsNullOrWhiteSpace(txtTempDir.Text)
                ? Path.Combine(Path.GetTempPath(), "RapidDownloader")
                : txtTempDir.Text.Trim();
            settings.SetFileCreationDateFromServer = chkSetCreationDate.Checked;

            // Segments Settings
            settings.AllowMoreThan32Connections = chkAllowMore32.Checked;
            settings.DefaultMaxConnections = (int)numDefaultConnections.Value;
            settings.ServerExceptions = _workingExceptions
                .Select(e => new ServerConnectionException(e.ServerPattern, e.Connections))
                .ToList();

            // Limits Settings
            settings.EnableDownloadLimits = chkEnableLimits.Checked;
            settings.DownloadLimitMBytes = (int)numLimitMBytes.Value;
            settings.DownloadLimitHours = (int)numLimitHours.Value;
            settings.WarnBeforeStoppingLimit = chkWarnBeforeStop.Checked;

            // DNS Settings
            settings.SelectedDnsProvider = cboDns.SelectedItem?.ToString() ?? DnsCatalog.SystemDnsName;
            settings.CustomDns = new CustomDnsConfig
            {
                Name = _workingCustomDns.Name,
                PrimaryIp = _workingCustomDns.PrimaryIp,
                SecondaryIp = _workingCustomDns.SecondaryIp,
                DohUrl = _workingCustomDns.DohUrl
            };

            settings.Save();
            AppLogger.Info($"[Options] Applied settings: MaxConn={settings.DefaultMaxConnections}, DNS='{settings.SelectedDnsProvider}', TempDir='{settings.TempDirectory}'");
        }
    }
}