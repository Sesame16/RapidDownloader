using System;
using System.Drawing;
using System.Windows.Forms;

namespace Rapid_Downloader
{
    public enum DeleteActionChoice
    {
        Cancel,
        RemoveFromListOnly,
        DeletePermanently
    }

    public class DeleteConfirmForm : Form
    {
        public DeleteActionChoice UserChoice { get; private set; } = DeleteActionChoice.Cancel;

        public DeleteConfirmForm(string fileName, Icon? appIcon)
        {
            InitializeComponent(fileName, appIcon);
        }

        private void InitializeComponent(string fileName, Icon? appIcon)
        {
            this.Text = "Delete Download";
            this.Size = new Size(500, 210);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            if (appIcon != null) this.Icon = appIcon;

            var picWarning = new PictureBox
            {
                Image = SystemIcons.Question.ToBitmap(),
                SizeMode = PictureBoxSizeMode.CenterImage,
                Location = new Point(20, 25),
                Size = new Size(48, 48)
            };

            var lblPrompt = new Label
            {
                Text = $"Are you sure you want to delete '{fileName}'?\n\nSelect 'Remove from list' to keep the file on your disk, or 'Yes but permanently' to erase the downloaded file.",
                Location = new Point(80, 20),
                Size = new Size(385, 75),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            var btnListOnly = new Button
            {
                Text = "Remove from list",
                Size = new Size(130, 32),
                Location = new Point(35, 115),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
            };
            btnListOnly.Click += (s, e) =>
            {
                UserChoice = DeleteActionChoice.RemoveFromListOnly;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            var btnPermanent = new Button
            {
                Text = "Yes but permanently",
                Size = new Size(150, 32),
                Location = new Point(175, 115),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.DarkRed
            };
            btnPermanent.Click += (s, e) =>
            {
                UserChoice = DeleteActionChoice.DeletePermanently;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(110, 32),
                Location = new Point(335, 115),
                DialogResult = DialogResult.Cancel,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
            };
            btnCancel.Click += (s, e) =>
            {
                UserChoice = DeleteActionChoice.Cancel;
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            this.Controls.AddRange(new Control[] { picWarning, lblPrompt, btnListOnly, btnPermanent, btnCancel });
            this.CancelButton = btnCancel;
        }
    }
}