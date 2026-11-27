using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Rapid_Downloader
{
    public class AdvancedTabControl : UserControl, IOptionsTab
    {
        // Logging Controls
        private CheckBox chkEnableLogging = null!;

        // User-Agent Controls
        private CheckBox chkEnableCustomUa = null!;
        private Label lblPreset = null!;
        private ComboBox cboUserAgentPreset = null!;
        private Button btnSavePreset = null!;
        private Button btnDeletePreset = null!;
        private Label lblCustomUaString = null!;
        private TextBox txtUserAgentString = null!;
        private Label lblUaNote = null!;

        private List<UserAgentPreset> _allPresets = new();
        private bool _isUpdatingUi = false;

        public AdvancedTabControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.White;
            this.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            // 1. Top Section Banner
            var bannerPanel = new Panel
            {
                Location = new Point(16, 12),
                Size = new Size(640, 36)
            };

            var picIcon = new PictureBox
            {
                Image = SystemIcons.Information.ToBitmap(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(4, 4),
                Size = new Size(28, 28)
            };

            var lblHeader = new Label
            {
                Text = "Advanced Application Settings",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Location = new Point(40, 7),
                AutoSize = true,
                ForeColor = Color.FromArgb(40, 40, 40)
            };

            bannerPanel.Controls.AddRange(new Control[] { picIcon, lblHeader });

            // 2. "Diagnostics & Logging" GroupBox
            var grpLogging = new GroupBox
            {
                Text = "Diagnostics && Logging",
                Location = new Point(16, 54),
                Size = new Size(640, 112),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            chkEnableLogging = new CheckBox
            {
                Text = "Enable application event and network logging",
                Location = new Point(18, 26),
                AutoSize = true,
                Checked = true
            };

            var lblLoggingDesc = new Label
            {
                Text = "Records HTTP probes, Cloudflare verification steps, segment worker progress, and errors in the Logs window.\n" +
                       "Disable logging to reduce memory overhead during large or unattended download queues.",
                Location = new Point(38, 54),
                Size = new Size(584, 44),
                ForeColor = Color.FromArgb(90, 90, 90)
            };

            grpLogging.Controls.AddRange(new Control[] { chkEnableLogging, lblLoggingDesc });

            // 3. "Custom User-Agent Override" GroupBox
            var grpUserAgent = new GroupBox
            {
                Text = "Custom User-Agent Override",
                Location = new Point(16, 178),
                Size = new Size(640, 292),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            chkEnableCustomUa = new CheckBox
            {
                Text = "Override default User-Agent header for HTTP requests and browser sessions",
                Location = new Point(18, 26),
                AutoSize = true
            };
            chkEnableCustomUa.CheckedChanged += (s, e) => UpdateUserAgentControlsState();

            lblPreset = new Label
            {
                Text = "Preset / Saved User-Agent:",
                Location = new Point(18, 58),
                AutoSize = true
            };

            cboUserAgentPreset = new ComboBox
            {
                Location = new Point(18, 80),
                Size = new Size(490, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboUserAgentPreset.SelectedIndexChanged += CboUserAgentPreset_SelectedIndexChanged;

            btnSavePreset = new Button
            {
                Text = "Add...",
                Location = new Point(520, 78),
                Size = new Size(102, 28)
            };
            btnSavePreset.Click += BtnSavePreset_Click;

            btnDeletePreset = new Button
            {
                Text = "Delete",
                Location = new Point(520, 112),
                Size = new Size(102, 28),
                Enabled = false
            };
            btnDeletePreset.Click += BtnDeletePreset_Click;

            lblCustomUaString = new Label
            {
                Text = "Active User-Agent string (select a preset or type manually):",
                Location = new Point(18, 118),
                AutoSize = true
            };

            txtUserAgentString = new TextBox
            {
                Location = new Point(18, 144),
                Size = new Size(604, 76),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            txtUserAgentString.TextChanged += TxtUserAgentString_TextChanged;

            lblUaNote = new Label
            {
                Text = "Tip: You can pick a built-in browser/CLI User-Agent, type a custom string directly in the box above, or click 'Add...' to save your own preset for future reuse.",
                Location = new Point(18, 230),
                Size = new Size(604, 46),
                ForeColor = Color.FromArgb(90, 90, 90)
            };

            grpUserAgent.Controls.AddRange(new Control[]
            {
                chkEnableCustomUa,
                lblPreset,
                cboUserAgentPreset,
                btnSavePreset,
                btnDeletePreset,
                lblCustomUaString,
                txtUserAgentString,
                lblUaNote
            });

            this.Controls.AddRange(new Control[]
            {
                bannerPanel,
                grpLogging,
                grpUserAgent
            });
        }

        public void LoadSettings(AppSettings settings)
        {
            _isUpdatingUi = true;
            try
            {
                chkEnableLogging.Checked = settings.EnableLogging;
                chkEnableCustomUa.Checked = settings.EnableCustomUserAgent;

                RebuildPresetsList(settings.SavedUserAgents);
                PopulatePresetDropdown(settings.SelectedUserAgentPreset);

                if (!string.IsNullOrWhiteSpace(settings.CustomUserAgent))
                {
                    txtUserAgentString.Text = settings.CustomUserAgent;
                }

                UpdateUserAgentControlsState();
            }
            finally
            {
                _isUpdatingUi = false;
            }
        }

        public bool ValidateSettings(out string errorMessage)
        {
            if (chkEnableCustomUa.Checked && string.IsNullOrWhiteSpace(txtUserAgentString.Text))
            {
                errorMessage = "Please enter or select a User-Agent string when Custom User-Agent Override is enabled.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public void ApplySettings(AppSettings settings)
        {
            settings.EnableLogging = chkEnableLogging.Checked;
            settings.EnableCustomUserAgent = chkEnableCustomUa.Checked;
            settings.SelectedUserAgentPreset = cboUserAgentPreset.SelectedItem?.ToString() ?? UserAgentCatalog.CustomEntryName;
            settings.CustomUserAgent = txtUserAgentString.Text.Trim();

            settings.SavedUserAgents = _allPresets
                .Where(p => !p.IsBuiltIn)
                .Select(p => new UserAgentPreset(p.Name, p.UserAgent, false))
                .ToList();
        }

        private void RebuildPresetsList(IEnumerable<UserAgentPreset> userSavedPresets)
        {
            _allPresets = UserAgentCatalog.BuiltInPresets
                .Select(p => new UserAgentPreset(p.Name, p.UserAgent, true))
                .ToList();

            foreach (var custom in userSavedPresets)
            {
                if (!string.IsNullOrWhiteSpace(custom.Name) &&
                    !_allPresets.Any(p => p.Name.Equals(custom.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    _allPresets.Add(new UserAgentPreset(custom.Name, custom.UserAgent, false));
                }
            }
        }

        private void PopulatePresetDropdown(string? selectName = null)
        {
            cboUserAgentPreset.Items.Clear();
            foreach (var preset in _allPresets)
            {
                cboUserAgentPreset.Items.Add(preset.Name);
            }
            cboUserAgentPreset.Items.Add(UserAgentCatalog.CustomEntryName);

            if (!string.IsNullOrEmpty(selectName) && cboUserAgentPreset.Items.Contains(selectName))
                cboUserAgentPreset.SelectedItem = selectName;
            else
                cboUserAgentPreset.SelectedIndex = 0;
        }

        private void CboUserAgentPreset_SelectedIndexChanged(object? sender, EventArgs e)
        {
            string selectedName = cboUserAgentPreset.SelectedItem?.ToString() ?? string.Empty;
            var preset = _allPresets.FirstOrDefault(p => p.Name == selectedName);

            if (preset != null)
            {
                if (!_isUpdatingUi)
                {
                    _isUpdatingUi = true;
                    txtUserAgentString.Text = preset.UserAgent;
                    _isUpdatingUi = false;
                }
                btnDeletePreset.Enabled = chkEnableCustomUa.Checked && !preset.IsBuiltIn;
            }
            else
            {
                btnDeletePreset.Enabled = false;
            }
        }

        private void TxtUserAgentString_TextChanged(object? sender, EventArgs e)
        {
            if (_isUpdatingUi) return;

            string currentText = txtUserAgentString.Text.Trim();
            var matchingPreset = _allPresets.FirstOrDefault(p => p.UserAgent.Equals(currentText, StringComparison.Ordinal));

            _isUpdatingUi = true;
            if (matchingPreset != null)
            {
                cboUserAgentPreset.SelectedItem = matchingPreset.Name;
                btnDeletePreset.Enabled = chkEnableCustomUa.Checked && !matchingPreset.IsBuiltIn;
            }
            else
            {
                cboUserAgentPreset.SelectedItem = UserAgentCatalog.CustomEntryName;
                btnDeletePreset.Enabled = false;
            }
            _isUpdatingUi = false;
        }

        private void BtnSavePreset_Click(object? sender, EventArgs e)
        {
            Icon? parentIcon = this.FindForm()?.Icon;
            using var dlg = new UserAgentPresetDialog(txtUserAgentString.Text.Trim(), parentIcon);
            if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                if (_allPresets.Any(p => p.IsBuiltIn && p.Name.Equals(dlg.PresetName, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("That name is reserved by a built-in preset. Please choose another name.", "Reserved Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var existingCustom = _allPresets.FirstOrDefault(p => !p.IsBuiltIn && p.Name.Equals(dlg.PresetName, StringComparison.OrdinalIgnoreCase));
                if (existingCustom != null)
                {
                    existingCustom.UserAgent = dlg.UserAgentValue;
                }
                else
                {
                    _allPresets.Add(new UserAgentPreset(dlg.PresetName, dlg.UserAgentValue, false));
                }

                _isUpdatingUi = true;
                txtUserAgentString.Text = dlg.UserAgentValue;
                PopulatePresetDropdown(dlg.PresetName);
                _isUpdatingUi = false;

                btnDeletePreset.Enabled = chkEnableCustomUa.Checked;
            }
        }

        private void BtnDeletePreset_Click(object? sender, EventArgs e)
        {
            string selectedName = cboUserAgentPreset.SelectedItem?.ToString() ?? string.Empty;
            var preset = _allPresets.FirstOrDefault(p => p.Name == selectedName && !p.IsBuiltIn);
            if (preset == null) return;

            var confirm = MessageBox.Show($"Delete saved User-Agent preset '{preset.Name}'?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.Yes)
            {
                _allPresets.Remove(preset);
                PopulatePresetDropdown(_allPresets[0].Name);
            }
        }

        private void UpdateUserAgentControlsState()
        {
            bool enabled = chkEnableCustomUa.Checked;
            lblPreset.Enabled = enabled;
            cboUserAgentPreset.Enabled = enabled;
            btnSavePreset.Enabled = enabled;
            lblCustomUaString.Enabled = enabled;
            txtUserAgentString.Enabled = enabled;

            string selectedName = cboUserAgentPreset.SelectedItem?.ToString() ?? string.Empty;
            var preset = _allPresets.FirstOrDefault(p => p.Name == selectedName);
            btnDeletePreset.Enabled = enabled && preset != null && !preset.IsBuiltIn;
        }
    }

    internal class UserAgentPresetDialog : Form
    {
        private TextBox txtName = null!;
        private TextBox txtUserAgent = null!;
        private Button btnOk = null!;
        private Button btnCancel = null!;

        public string PresetName => txtName.Text.Trim();
        public string UserAgentValue => txtUserAgent.Text.Trim();

        public UserAgentPresetDialog(string initialUserAgent, Icon? appIcon)
        {
            InitializeComponent(appIcon);
            txtUserAgent.Text = initialUserAgent;
        }

        private void InitializeComponent(Icon? appIcon)
        {
            this.Text = "Add Custom User-Agent Preset";
            this.Size = new Size(490, 275);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            if (appIcon != null) this.Icon = appIcon;

            var lblName = new Label
            {
                Text = "Preset Display Name (e.g. Custom Firefox or Mobile Bot):",
                Location = new Point(20, 18),
                AutoSize = true
            };

            txtName = new TextBox
            {
                Location = new Point(20, 40),
                Size = new Size(435, 23),
                PlaceholderText = "My Custom User-Agent"
            };

            var lblUa = new Label
            {
                Text = "User-Agent Header Value:",
                Location = new Point(20, 76),
                AutoSize = true
            };

            txtUserAgent = new TextBox
            {
                Location = new Point(20, 98),
                Size = new Size(435, 75),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                PlaceholderText = "Mozilla/5.0 ..."
            };

            btnOk = new Button
            {
                Text = "OK",
                Size = new Size(85, 28),
                Location = new Point(275, 190),
                DialogResult = DialogResult.OK
            };
            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Please enter a preset name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None;
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtUserAgent.Text))
                {
                    MessageBox.Show("Please enter a User-Agent string.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None;
                    return;
                }
                this.Close();
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(85, 28),
                Location = new Point(370, 190),
                DialogResult = DialogResult.Cancel
            };
            btnCancel.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[] { lblName, txtName, lblUa, txtUserAgent, btnOk, btnCancel });
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }
    }
}