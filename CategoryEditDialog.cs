using System;
using System.Drawing;
using System.Windows.Forms;

namespace Rapid_Downloader
{
    public class CategoryEditDialog : Form
    {
        private TextBox txtName = null!;
        private TextBox txtExtensions = null!;
        private Button btnOk = null!;
        private Button btnCancel = null!;

        public string CategoryName => txtName.Text.Trim();
        public string Extensions => txtExtensions.Text.Trim();

        public CategoryEditDialog(string name = "", string extensions = "", bool isEditing = false, Icon? appIcon = null)
        {
            InitializeComponent(isEditing, appIcon);
            txtName.Text = name;
            txtExtensions.Text = extensions;

            if (isEditing && (name == "Others" || name == "All Downloads"))
            {
                txtName.Enabled = false; // System default category names are locked
            }
        }

        private void InitializeComponent(bool isEditing, Icon? appIcon)
        {
            this.Text = isEditing ? "Edit Category" : "New Category";
            this.Size = new Size(460, 240);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            if (appIcon != null) this.Icon = appIcon;

            var lblName = new Label
            {
                Text = "Category Name:",
                Location = new Point(20, 20),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            txtName = new TextBox
            {
                Location = new Point(20, 42),
                Size = new Size(405, 23),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            var lblExt = new Label
            {
                Text = "File Extensions (separated by spaces):",
                Location = new Point(20, 78),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            txtExtensions = new TextBox
            {
                Location = new Point(20, 100),
                Size = new Size(405, 23),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                PlaceholderText = "e.g. zip rar 7z tar gz"
            };

            btnOk = new Button
            {
                Text = "OK",
                Size = new Size(85, 28),
                Location = new Point(245, 150),
                DialogResult = DialogResult.OK
            };
            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Please enter a category name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None;
                    return;
                }
                this.Close();
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(85, 28),
                Location = new Point(340, 150),
                DialogResult = DialogResult.Cancel
            };
            btnCancel.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[] { lblName, txtName, lblExt, txtExtensions, btnOk, btnCancel });
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }
    }
}