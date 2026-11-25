using System;
using System.Drawing;
using System.Windows.Forms;

namespace Rapid_Downloader
{
    public class ServerExceptionDialog : Form
    {
        private TextBox txtServer = null!;
        private NumericUpDown numConnections = null!;
        private Button btnOk = null!;
        private Button btnCancel = null!;

        public string ServerPattern => txtServer.Text.Trim();
        public int Connections => (int)numConnections.Value;

        public ServerExceptionDialog(string server = "", int connections = 4, int maxConnections = 32, bool isEditing = false, Icon? appIcon = null)
        {
            InitializeComponent(maxConnections, isEditing, appIcon);
            txtServer.Text = server;
            numConnections.Value = Math.Clamp(connections, 1, maxConnections);
        }

        private void InitializeComponent(int maxConnections, bool isEditing, Icon? appIcon)
        {
            this.Text = isEditing ? "Edit Server Exception" : "Add Server Exception";
            this.Size = new Size(440, 230);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            if (appIcon != null) this.Icon = appIcon;

            var lblServer = new Label
            {
                Text = "Server hostname or wildcard (e.g. *.github.com or files.example.org):",
                Location = new Point(20, 20),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            txtServer = new TextBox
            {
                Location = new Point(20, 44),
                Size = new Size(385, 23),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                PlaceholderText = "*.example.com"
            };

            var lblConn = new Label
            {
                Text = "Max parallel connections for this server:",
                Location = new Point(20, 86),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            numConnections = new NumericUpDown
            {
                Location = new Point(265, 83),
                Size = new Size(80, 23),
                Minimum = 1,
                Maximum = maxConnections,
                Value = 4,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            btnOk = new Button
            {
                Text = "OK",
                Size = new Size(85, 28),
                Location = new Point(225, 140),
                DialogResult = DialogResult.OK
            };
            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtServer.Text))
                {
                    MessageBox.Show("Please enter a server hostname or wildcard pattern.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None;
                    return;
                }
                this.Close();
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(85, 28),
                Location = new Point(320, 140),
                DialogResult = DialogResult.Cancel
            };
            btnCancel.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[] { lblServer, txtServer, lblConn, numConnections, btnOk, btnCancel });
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }
    }
}