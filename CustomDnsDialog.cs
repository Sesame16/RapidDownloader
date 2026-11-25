using System;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Rapid_Downloader
{
    public class CustomDnsDialog : Form
    {
        private TextBox txtName = null!;
        private TextBox txtPrimaryIp = null!;
        private TextBox txtSecondaryIp = null!;
        private TextBox txtDohUrl = null!;
        private Button btnTest = null!;
        private Label lblTestResult = null!;
        private Button btnOk = null!;
        private Button btnCancel = null!;

        public CustomDnsConfig ResultConfig { get; private set; }

        public CustomDnsDialog(CustomDnsConfig existing, Icon? appIcon = null)
        {
            ResultConfig = new CustomDnsConfig
            {
                Name = existing.Name,
                PrimaryIp = existing.PrimaryIp,
                SecondaryIp = existing.SecondaryIp,
                DohUrl = existing.DohUrl
            };

            InitializeComponent(appIcon);

            txtName.Text = ResultConfig.Name;
            txtPrimaryIp.Text = ResultConfig.PrimaryIp;
            txtSecondaryIp.Text = ResultConfig.SecondaryIp;
            txtDohUrl.Text = ResultConfig.DohUrl;
        }

        private void InitializeComponent(Icon? appIcon)
        {
            this.Text = "Custom DNS Settings";
            this.Size = new Size(500, 340);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            if (appIcon != null) this.Icon = appIcon;

            var lblName = new Label
            {
                Text = "DNS Profile Name:",
                Location = new Point(20, 20),
                AutoSize = true
            };

            txtName = new TextBox
            {
                Location = new Point(20, 42),
                Size = new Size(440, 23),
                PlaceholderText = "e.g. Custom NextDNS"
            };

            var lblPrimary = new Label
            {
                Text = "Primary DNS (IPv4):",
                Location = new Point(20, 78),
                AutoSize = true
            };

            txtPrimaryIp = new TextBox
            {
                Location = new Point(20, 100),
                Size = new Size(210, 23),
                PlaceholderText = "1.1.1.1"
            };

            var lblSecondary = new Label
            {
                Text = "Secondary DNS (IPv4, optional):",
                Location = new Point(250, 78),
                AutoSize = true
            };

            txtSecondaryIp = new TextBox
            {
                Location = new Point(250, 100),
                Size = new Size(210, 23),
                PlaceholderText = "1.0.0.1"
            };

            var lblDoh = new Label
            {
                Text = "DNS-over-HTTPS (DoH) Template URL (used by Chromium engine):",
                Location = new Point(20, 138),
                AutoSize = true
            };

            txtDohUrl = new TextBox
            {
                Location = new Point(20, 160),
                Size = new Size(440, 23),
                PlaceholderText = "https://dns.nextdns.io/your-profile-id"
            };

            btnTest = new Button
            {
                Text = "Test DNS",
                Location = new Point(20, 200),
                Size = new Size(95, 28)
            };
            btnTest.Click += async (s, e) => await TestDnsAsync();

            lblTestResult = new Label
            {
                Text = "Enter an IP or DoH endpoint and click Test DNS.",
                Location = new Point(125, 206),
                Size = new Size(335, 20),
                ForeColor = Color.DimGray
            };

            btnOk = new Button
            {
                Text = "OK",
                Location = new Point(280, 252),
                Size = new Size(85, 28)
            };
            btnOk.Click += BtnOk_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(375, 252),
                Size = new Size(85, 28),
                DialogResult = DialogResult.Cancel
            };
            btnCancel.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[]
            {
                lblName, txtName,
                lblPrimary, txtPrimaryIp,
                lblSecondary, txtSecondaryIp,
                lblDoh, txtDohUrl,
                btnTest, lblTestResult,
                btnOk, btnCancel
            });

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private async Task TestDnsAsync()
        {
            string doh = txtDohUrl.Text.Trim();
            string primaryIp = txtPrimaryIp.Text.Trim();

            btnTest.Enabled = false;
            lblTestResult.ForeColor = Color.DimGray;
            lblTestResult.Text = "Testing DNS latency...";

            try
            {
                var sw = Stopwatch.StartNew();

                if (!string.IsNullOrWhiteSpace(doh))
                {
                    if (!Uri.TryCreate(doh, UriKind.Absolute, out Uri? dohUri) || dohUri == null || dohUri.Scheme != Uri.UriSchemeHttps)
                    {
                        lblTestResult.ForeColor = Color.Crimson;
                        lblTestResult.Text = "Invalid DoH URL (must start with https://).";
                        return;
                    }

                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    var testUri = new Uri($"{doh.TrimEnd('/')}?name=example.com&type=A");
                    using var req = new HttpRequestMessage(HttpMethod.Get, testUri);
                    req.Headers.Add("Accept", "application/dns-json");

                    using var resp = await client.SendAsync(req);
                    sw.Stop();

                    lblTestResult.ForeColor = Color.FromArgb(16, 124, 65);
                    lblTestResult.Text = $"DoH endpoint reachable ({(int)resp.StatusCode}) in {sw.ElapsedMilliseconds} ms";
                }
                else if (IPAddress.TryParse(primaryIp, out IPAddress? ip) && ip != null)
                {
                    using var tcp = new TcpClient();
                    var connectTask = tcp.ConnectAsync(ip, 53);
                    var timeoutTask = Task.Delay(3000);
                    var completed = await Task.WhenAny(connectTask, timeoutTask);
                    sw.Stop();

                    if (completed == connectTask && tcp.Connected)
                    {
                        lblTestResult.ForeColor = Color.FromArgb(16, 124, 65);
                        lblTestResult.Text = $"DNS server {ip} responded in {sw.ElapsedMilliseconds} ms";
                    }
                    else
                    {
                        lblTestResult.ForeColor = Color.Crimson;
                        lblTestResult.Text = $"Timed out reaching {ip}:53.";
                    }
                }
                else
                {
                    lblTestResult.ForeColor = Color.Crimson;
                    lblTestResult.Text = "Enter a valid Primary IPv4 address or HTTPS DoH URL.";
                }
            }
            catch (Exception ex)
            {
                lblTestResult.ForeColor = Color.Crimson;
                lblTestResult.Text = $"Test failed: {ex.Message}";
            }
            finally
            {
                btnTest.Enabled = true;
            }
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            string primary = txtPrimaryIp.Text.Trim();
            string secondary = txtSecondaryIp.Text.Trim();
            string doh = txtDohUrl.Text.Trim();

            if (string.IsNullOrWhiteSpace(primary) && string.IsNullOrWhiteSpace(doh))
            {
                MessageBox.Show("Please enter at least a Primary DNS IP or a DNS-over-HTTPS URL.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!string.IsNullOrWhiteSpace(primary) && !IPAddress.TryParse(primary, out _))
            {
                MessageBox.Show("Primary DNS must be a valid IPv4/IPv6 address.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!string.IsNullOrWhiteSpace(secondary) && !IPAddress.TryParse(secondary, out _))
            {
                MessageBox.Show("Secondary DNS must be a valid IPv4/IPv6 address.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!string.IsNullOrWhiteSpace(doh) && (!Uri.TryCreate(doh, UriKind.Absolute, out Uri? u) || u == null || u.Scheme != Uri.UriSchemeHttps))
            {
                MessageBox.Show("DoH URL must be a valid HTTPS address.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ResultConfig = new CustomDnsConfig
            {
                Name = string.IsNullOrWhiteSpace(txtName.Text) ? "Custom DNS" : txtName.Text.Trim(),
                PrimaryIp = primary,
                SecondaryIp = secondary,
                DohUrl = doh
            };

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}