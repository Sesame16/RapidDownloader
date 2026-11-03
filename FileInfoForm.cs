using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Rapid_Downloader
{
    public class FileInfoForm : Form
    {
        private TextBox txtFileName = null!;
        private TextBox txtFileSize = null!;
        private ComboBox cboCategory = null!;
        private TextBox txtSavePath = null!;
        private Button btnBrowse = null!;
        private Button btnStartDownload = null!;
        private Button btnCancel = null!;

        public DownloadInfo FinalInfo { get; private set; }

        public FileInfoForm(DownloadInfo info)
        {
            this.FinalInfo = info;
            InitializeComponent();
            PopulateData();
        }

        private void InitializeComponent()
        {
            this.Text = "File Info";
            this.Size = new Size(540, 310);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            int leftCol = 20;
            int rightCol = 140;

            var lblName = new Label { Text = "File Name:", Location = new Point(leftCol, 25), AutoSize = true };
            txtFileName = new TextBox { Location = new Point(rightCol, 22), Size = new Size(355, 23) };

            var lblSize = new Label { Text = "File Size:", Location = new Point(leftCol, 65), AutoSize = true };
            txtFileSize = new TextBox { Location = new Point(rightCol, 62), Size = new Size(180, 23), ReadOnly = true };

            var lblCategory = new Label { Text = "Category:", Location = new Point(leftCol, 105), AutoSize = true };
            cboCategory = new ComboBox { Location = new Point(rightCol, 102), Size = new Size(180, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            cboCategory.Items.AddRange(new object[] { "Documents", "Media", "Applications", "Archived", "Others" });

            var lblSave = new Label { Text = "Save Location:", Location = new Point(leftCol, 145), AutoSize = true };
            txtSavePath = new TextBox { Location = new Point(rightCol, 142), Size = new Size(265, 23) };
            btnBrowse = new Button { Text = "Browse...", Location = new Point(415, 140), Size = new Size(80, 26) };
            btnBrowse.Click += (s, e) =>
            {
                using var fbd = new FolderBrowserDialog { SelectedPath = txtSavePath.Text };
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    txtSavePath.Text = fbd.SelectedPath;
                }
            };

            btnStartDownload = new Button { Text = "Start Download", Location = new Point(275, 220), Size = new Size(120, 32), DialogResult = DialogResult.OK };
            btnStartDownload.Click += (s, e) =>
            {
                FinalInfo.FileName = txtFileName.Text.Trim();
                FinalInfo.Category = cboCategory.SelectedItem?.ToString() ?? "Others";
                FinalInfo.SaveDirectory = txtSavePath.Text.Trim();
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            btnCancel = new Button { Text = "Cancel", Location = new Point(405, 220), Size = new Size(90, 32), DialogResult = DialogResult.Cancel };
            btnCancel.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            this.Controls.AddRange(new Control[] {
                lblName, txtFileName,
                lblSize, txtFileSize,
                lblCategory, cboCategory,
                lblSave, txtSavePath, btnBrowse,
                btnStartDownload, btnCancel
            });

            this.AcceptButton = btnStartDownload;
            this.CancelButton = btnCancel;
        }

        private void PopulateData()
        {
            txtFileName.Text = FinalInfo.FileName;
            txtFileSize.Text = FormatBytes(FinalInfo.TotalBytes);
            cboCategory.SelectedItem = FinalInfo.Category;
            txtSavePath.Text = FinalInfo.SaveDirectory;
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "Unknown";
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double d = bytes;
            int i = 0;
            while (d >= 1024 && i < units.Length - 1)
            {
                d /= 1024;
                i++;
            }
            return $"{d:0.##} {units[i]}";
        }
    }
}