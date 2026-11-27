using System.Drawing;
using System.Windows.Forms;

namespace Rapid_Downloader
{
    public class ProxyTabControl : UserControl, IOptionsTab
    {
        public ProxyTabControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.White;
            this.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            // Top Section Banner
            var bannerPanel = new Panel
            {
                Location = new Point(16, 12),
                Size = new Size(640, 36)
            };

            var picIcon = new PictureBox
            {
                Image = SystemIcons.Shield.ToBitmap(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(4, 4),
                Size = new Size(28, 28)
            };

            var lblHeader = new Label
            {
                Text = "Proxy & Network Routing",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Location = new Point(40, 7),
                AutoSize = true,
                ForeColor = Color.FromArgb(40, 40, 40)
            };

            bannerPanel.Controls.AddRange(new Control[] { picIcon, lblHeader });

            // Placeholder Container
            var grpProxyPlaceholder = new GroupBox
            {
                Text = "Proxy Configuration",
                Location = new Point(16, 54),
                Size = new Size(640, 140),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            var lblDescription = new Label
            {
                Text = "Configure direct connections, Windows system proxy auto-detection, or manual HTTP, HTTPS, and SOCKS5 proxy servers with authentication.\n\n" +
                       "(Awaiting layout and feature specifications for this tab.)",
                Location = new Point(18, 30),
                Size = new Size(600, 85),
                ForeColor = Color.FromArgb(90, 90, 90)
            };

            grpProxyPlaceholder.Controls.Add(lblDescription);

            this.Controls.AddRange(new Control[] { bannerPanel, grpProxyPlaceholder });
        }

        public void LoadSettings(AppSettings settings)
        {
            // Ready for Proxy settings binding
        }

        public bool ValidateSettings(out string errorMessage)
        {
            errorMessage = string.Empty;
            return true;
        }

        public void ApplySettings(AppSettings settings)
        {
            // Ready for Proxy settings persistence
        }
    }
}