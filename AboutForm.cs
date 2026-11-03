using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Rapid_Downloader
{
    public class AboutForm : Form
    {
        public AboutForm(Icon? appIcon)
        {
            InitializeComponent(appIcon);
        }

        private void InitializeComponent(Icon? appIcon)
        {
            this.Text = "About Rapid Downloader";
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.ClientSize = new Size(540, 240);
            this.MinimumSize = new Size(500, 240);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            if (appIcon != null)
            {
                this.Icon = appIcon;

                var pbIcon = new PictureBox
                {
                    Image = appIcon.ToBitmap(),
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Location = new Point(24, 28),
                    Size = new Size(52, 52)
                };
                this.Controls.Add(pbIcon);
            }

            int contentLeft = 95;

            var lblTitle = new Label
            {
                Text = "Rapid Downloader",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                Location = new Point(contentLeft, 22),
                AutoSize = true
            };

            var lblVersion = new Label
            {
                Text = "Version 1.0.0 (.NET 8/9 / VS Community)",
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = Color.DimGray,
                Location = new Point(contentLeft, 52),
                AutoSize = true
            };

            var lblDesc = new Label
            {
                Text = "Fast, lightweight multi-connection download manager.",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                Location = new Point(contentLeft, 82),
                MaximumSize = new Size(420, 0),
                AutoSize = true
            };

            var lblGithub = new Label
            {
                Text = "Source Repository:",
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(70, 70, 70),
                Location = new Point(contentLeft, 116),
                AutoSize = true
            };

            var linkGithub = new LinkLabel
            {
                Text = "https://github.com/Sesame16/Rapid-Downloder",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                Location = new Point(contentLeft, 138),
                AutoSize = true
            };
            linkGithub.LinkClicked += (s, e) =>
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = linkGithub.Text,
                    UseShellExecute = true
                });
            };

            var btnOk = new Button
            {
                Text = "OK",
                Location = new Point(420, 185),
                Size = new Size(95, 32),
                DialogResult = DialogResult.OK
            };
            btnOk.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[] { lblTitle, lblVersion, lblDesc, lblGithub, linkGithub, btnOk });
            this.AcceptButton = btnOk;
        }
    }
}