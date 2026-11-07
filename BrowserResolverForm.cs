using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Rapid_Downloader
{
    public class BrowserResolverForm : Form
    {
        private WebView2 webView = null!;
        private Label lblHeader = null!;
        private System.Windows.Forms.Timer _pollTimer = null!;
        private readonly string _targetUrl;
        private bool _isCompleted = false;
        private bool _navigationFinished = false;
        private int _postClearanceWaitTicks = 0;

        public string FinalUrl { get; private set; } = string.Empty;
        public string ResolvedCookies { get; private set; } = string.Empty;
        public string ResolvedUserAgent { get; private set; } = string.Empty;
        public long ResolvedTotalBytes { get; private set; } = -1;
        public string ResolvedFileName { get; private set; } = string.Empty;

        public BrowserResolverForm(string url, Icon? appIcon)
        {
            _targetUrl = url;
            InitializeComponent(appIcon);
        }

        private void InitializeComponent(Icon? appIcon)
        {
            this.Text = "Security Verification - Rapid Downloader";
            this.Size = new Size(580, 520);
            this.MinimumSize = new Size(480, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            if (appIcon != null) this.Icon = appIcon;

            lblHeader = new Label
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = Color.FromArgb(245, 245, 245),
                Text = "  Verifying server security challenge. Please complete any verification prompt below if required...",
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            webView = new WebView2
            {
                Dock = DockStyle.Fill
            };

            this.Controls.Add(webView);
            this.Controls.Add(lblHeader);

            _pollTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _pollTimer.Tick += async (s, e) => await CheckCookiesAsync();

            this.Load += BrowserResolverForm_Load;
        }

        private async void BrowserResolverForm_Load(object? sender, EventArgs e)
        {
            try
            {
                AppLogger.Info("[WebView2] Initializing CoreWebView2 environment...");
                var env = await WebView2Helper.GetEnvironmentAsync();
                await webView.EnsureCoreWebView2Async(env);

                ResolvedUserAgent = webView.CoreWebView2.Settings.UserAgent;
                AppLogger.Info($"[WebView2] Chromium User-Agent: {ResolvedUserAgent}");

                webView.SourceChanged += (s, args) =>
                {
                    FinalUrl = webView.Source?.AbsoluteUri ?? string.Empty;
                };

                webView.CoreWebView2.NavigationCompleted += (s, args) =>
                {
                    _navigationFinished = true;
                };

                // Intercept download metadata directly from Chromium
                webView.CoreWebView2.DownloadStarting += (s, args) =>
                {
                    args.Cancel = true; // Probe only; do not write to disk yet

                    string suggestedPath = !string.IsNullOrWhiteSpace(args.ResultFilePath)
                        ? args.ResultFilePath
                        : (args.DownloadOperation?.ResultFilePath ?? string.Empty);

                    ResolvedFileName = Path.GetFileName(suggestedPath);

                    if (args.DownloadOperation?.TotalBytesToReceive is ulong totalBytes && totalBytes > 0)
                    {
                        ResolvedTotalBytes = (long)totalBytes;
                    }

                    AppLogger.Info($"[WebView2] Captured download info: '{ResolvedFileName}', Size: {ResolvedTotalBytes} bytes");
                    _ = ExtractCookiesAndFinishAsync();
                };

                // Intercept Content-Length header from network response
                webView.CoreWebView2.WebResourceResponseReceived += (s, args) =>
                {
                    if (args.Response != null && (args.Response.StatusCode == 200 || args.Response.StatusCode == 206))
                    {
                        if (args.Response.Headers.Contains("Content-Length"))
                        {
                            string clStr = args.Response.Headers.GetHeader("Content-Length");
                            if (long.TryParse(clStr, out long cl) && cl > 0 && ResolvedTotalBytes <= 0)
                            {
                                ResolvedTotalBytes = cl;
                                AppLogger.Info($"[WebView2] Captured Content-Length header: {cl} bytes");
                            }
                        }
                    }
                };

                _pollTimer.Start();
                webView.CoreWebView2.Navigate(_targetUrl);
            }
            catch (Exception ex)
            {
                AppLogger.Error("[WebView2] Initialization failed", ex);
                MessageBox.Show($"Failed to initialize WebView2: {ex.Message}", "WebView2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }

        private async Task CheckCookiesAsync()
        {
            if (_isCompleted || webView.CoreWebView2 == null) return;

            try
            {
                var cookies = await webView.CoreWebView2.CookieManager.GetCookiesAsync(_targetUrl);
                var cfCookie = cookies.FirstOrDefault(c => c.Name == "cf_clearance");

                if (cfCookie != null)
                {
                    // If size has already been intercepted by DownloadStarting or headers, complete immediately
                    if (ResolvedTotalBytes > 0)
                    {
                        await ExtractCookiesAndFinishAsync();
                        return;
                    }

                    // Otherwise, allow up to 3 seconds for response headers to arrive
                    _postClearanceWaitTicks++;
                    if (_postClearanceWaitTicks >= 3 || _navigationFinished)
                    {
                        AppLogger.Info("[WebView2] cf_clearance verified and navigation settled.");
                        await ExtractCookiesAndFinishAsync();
                    }
                }
            }
            catch { }
        }

        private async Task ExtractCookiesAndFinishAsync()
        {
            if (_isCompleted || webView.CoreWebView2 == null) return;
            _isCompleted = true;
            _pollTimer.Stop();

            try
            {
                var cookies = await webView.CoreWebView2.CookieManager.GetCookiesAsync(_targetUrl);
                ResolvedCookies = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
                AppLogger.Info($"[WebView2] Exported {cookies.Count} cookie(s). Ready.");

                this.BeginInvoke(new Action(() =>
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }));
            }
            catch (Exception ex)
            {
                AppLogger.Error("[WebView2] Failed to export cookies", ex);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _pollTimer?.Stop();
            _pollTimer?.Dispose();
            webView?.Dispose();
            base.OnFormClosed(e);
        }
    }
}