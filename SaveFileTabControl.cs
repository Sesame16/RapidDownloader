using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Rapid_Downloader
{
    public class SaveFileTabControl : UserControl, IOptionsTab
    {
        private ComboBox cboCategory = null!;
        private Button btnNewCategory = null!;
        private Button btnEditCategory = null!;
        private Button btnDeleteCategory = null!;
        private Label lblAutoPutIn = null!;
        private TextBox txtFileTypes = null!;
        private CheckBox chkSetCreationDate = null!;
        private TextBox txtTempDir = null!;
        private Button btnBrowseTemp = null!;

        private List<CategoryConfig> _workingCategories = new();

        public SaveFileTabControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.White;
            this.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            // 1. Top Section Header
            var bannerPanel = new Panel
            {
                Location = new Point(16, 12),
                Size = new Size(640, 36)
            };

            var picFolder = new PictureBox
            {
                Image = SystemIcons.Application.ToBitmap(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(4, 4),
                Size = new Size(28, 28)
            };

            var lblHeader = new Label
            {
                Text = "Categories, file types, and storage",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Location = new Point(40, 7),
                AutoSize = true,
                ForeColor = Color.FromArgb(40, 40, 40)
            };

            bannerPanel.Controls.AddRange(new Control[] { picFolder, lblHeader });

            // 2. "Save To..." GroupBox
            var grpSaveTo = new GroupBox
            {
                Text = "Save To...",
                Location = new Point(16, 54),
                Size = new Size(640, 215),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            var lblCategory = new Label
            {
                Text = "Category:",
                Location = new Point(16, 26),
                AutoSize = true
            };

            cboCategory = new ComboBox
            {
                Location = new Point(16, 48),
                Size = new Size(490, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboCategory.SelectedIndexChanged += CboCategory_SelectedIndexChanged;

            btnNewCategory = new Button
            {
                Text = "New",
                Location = new Point(520, 24),
                Size = new Size(102, 28)
            };
            btnNewCategory.Click += BtnNewCategory_Click;

            btnEditCategory = new Button
            {
                Text = "Edit...",
                Location = new Point(520, 58),
                Size = new Size(102, 28)
            };
            btnEditCategory.Click += BtnEditCategory_Click;

            btnDeleteCategory = new Button
            {
                Text = "Delete",
                Location = new Point(520, 92),
                Size = new Size(102, 28)
            };
            btnDeleteCategory.Click += BtnDeleteCategory_Click;

            lblAutoPutIn = new Label
            {
                Text = "Automatically put in category the following file types:",
                Location = new Point(16, 94),
                Size = new Size(490, 20)
            };

            txtFileTypes = new TextBox
            {
                Location = new Point(16, 118),
                Size = new Size(490, 78),
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

            // 3. Server Timestamp Checkbox
            chkSetCreationDate = new CheckBox
            {
                Text = "Set file creation date as provided by the server",
                Location = new Point(20, 280),
                AutoSize = true
            };

            // 4. "Temporary Directory" GroupBox
            var grpTemp = new GroupBox
            {
                Text = "Temporary directory",
                Location = new Point(16, 314),
                Size = new Size(640, 155),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            txtTempDir = new TextBox
            {
                Location = new Point(16, 30),
                Size = new Size(490, 23)
            };

            btnBrowseTemp = new Button
            {
                Text = "Browse...",
                Location = new Point(520, 28),
                Size = new Size(102, 27)
            };
            btnBrowseTemp.Click += BtnBrowseTemp_Click;

            var lblTempDesc = new Label
            {
                Text = "Temporary directory is required for storing file parts during download.\n" +
                       "If you have several physical drives on your computer, selecting a different " +
                       "physical drive for the temporary directory and destination folders improves " +
                       "disk speed when assembling downloaded files.",
                Location = new Point(16, 68),
                Size = new Size(605, 72),
                ForeColor = Color.FromArgb(90, 90, 90)
            };

            grpTemp.Controls.AddRange(new Control[] { txtTempDir, btnBrowseTemp, lblTempDesc });

            this.Controls.AddRange(new Control[]
            {
                bannerPanel,
                grpSaveTo,
                chkSetCreationDate,
                grpTemp
            });
        }

        public void LoadSettings(AppSettings settings)
        {
            _workingCategories = settings.Categories
                .Select(c => new CategoryConfig(c.Name, c.Extensions, c.IsDefault))
                .ToList();

            PopulateCategoriesCombo();

            txtTempDir.Text = settings.TempDirectory;
            chkSetCreationDate.Checked = settings.SetFileCreationDateFromServer;
        }

        public bool ValidateSettings(out string errorMessage)
        {
            string tempPath = txtTempDir.Text.Trim();
            if (string.IsNullOrWhiteSpace(tempPath))
            {
                errorMessage = "Please specify a valid temporary directory path.";
                return false;
            }

            if (tempPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                errorMessage = "The temporary directory path contains invalid characters.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public void ApplySettings(AppSettings settings)
        {
            settings.Categories = _workingCategories
                .Select(c => new CategoryConfig(c.Name, c.Extensions, c.IsDefault))
                .ToList();

            settings.TempDirectory = string.IsNullOrWhiteSpace(txtTempDir.Text)
                ? Path.Combine(Path.GetTempPath(), "RapidDownloader")
                : txtTempDir.Text.Trim();

            settings.SetFileCreationDateFromServer = chkSetCreationDate.Checked;
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
            Icon? parentIcon = this.FindForm()?.Icon;
            using var dlg = new CategoryEditDialog("", "", false, parentIcon);
            if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
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

            Icon? parentIcon = this.FindForm()?.Icon;
            using var dlg = new CategoryEditDialog(cat.Name, cat.Extensions, true, parentIcon);
            if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                // Prevent renaming to another existing category's name
                if (!cat.Name.Equals(dlg.CategoryName, StringComparison.OrdinalIgnoreCase) &&
                    _workingCategories.Any(c => c.Name.Equals(dlg.CategoryName, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show($"Another category named '{dlg.CategoryName}' already exists.", "Duplicate Category", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

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

            if (fbd.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                txtTempDir.Text = fbd.SelectedPath;
            }
        }
    }
}