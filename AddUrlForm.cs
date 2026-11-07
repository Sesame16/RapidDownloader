using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Rapid_Downloader
{
    public class AddUrlForm : Form
    {
        private TextBox txtUrl = null!;
        private Button btnStart = null!;
        private Button btnCancel = null!;
        private Label lblStatus = null!;

        public DownloadInfo? ResultDownloadInfo { get; private set; }

        public AddUrlForm()
        {
            InitializeComponent();

            if (Clipboard.ContainsText())
            {
                string clip = Clipboard.GetText().Trim();
                if (Uri.TryCreate(clip, UriKind.Absolute, out Uri? uri) && uri != null &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    txtUrl.Text = clip;
                }
            }
        }

        private void InitializeComponent()
        {
            this.Text = "Add URL";
            this.Size = new Size(520, 180);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Label lblUrl = new Label
            {
                Text = "Download URL:",
                Location = new Point(16, 20),
                AutoSize = true
            };

            txtUrl = new TextBox
            {
                Location = new Point(16, 45),
                Size = new Size(470, 25)
            };

            lblStatus = new Label
            {
                Location = new Point(16, 75),
                Size = new Size(470, 20),
                ForeColor = Color.DimGray,
                Text = "Ready"
            };

            btnStart = new Button
            {
                Text = "Start",
                Location = new Point(310, 100),
                Size = new Size(85, 28)
            };
            btnStart.Click += async (s, e) => await HandleStartAsync();

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(402, 100),
                Size = new Size(85, 28),
                DialogResult = DialogResult.Cancel
            };
            btnCancel.Click += (s, e) => this.Close();

            this.Controls.Add(lblUrl);
            this.Controls.Add(txtUrl);
            this.Controls.Add(lblStatus);
            this.Controls.Add(btnStart);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnStart;
            this.CancelButton = btnCancel;
        }

        private async Task HandleStartAsync()
        {
            string url = txtUrl.Text.Trim();
            AppLogger.Info($"[AddURL] User clicked Start with target URL: {url}");

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uriResult) || uriResult == null ||
                (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
            {
                AppLogger.Warn($"[AddURL] Validation failed for invalid URL: {url}");
                MessageBox.Show("Please provide a valid HTTP or HTTPS download URL.", "Invalid URL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnStart.Enabled = false;
            lblStatus.Text = "Connecting and probing file information...";

            DownloadInfo? info = null;

            try
            {
                try
                {
                    info = await FetchRemoteFileInfoAsync(uriResult);
                }
                catch (CloudflareChallengeException)
                {
                    AppLogger.Warn("[AddURL] Cloudflare challenge detected. Launching Web Verification...");
                    lblStatus.Text = "Waiting for security verification...";

                    using var resolver = new BrowserResolverForm(uriResult.AbsoluteUri, this.Icon);
                    if (resolver.ShowDialog(this) == DialogResult.OK)
                    {
                        AppLogger.Info("[AddURL] Security verified. Processing file details...");

                        string resolvedName = !string.IsNullOrWhiteSpace(resolver.ResolvedFileName)
                            ? resolver.ResolvedFileName
                            : Path.GetFileName(uriResult.LocalPath);

                        if (string.IsNullOrWhiteSpace(resolvedName)) resolvedName = "downloaded_file.bin";

                        info = new DownloadInfo
                        {
                            Url = uriResult.AbsoluteUri,
                            FileName = resolvedName,
                            TotalBytes = resolver.ResolvedTotalBytes,
                            Category = CategorizeExtension(Path.GetExtension(resolvedName)),
                            SaveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                            Cookies = resolver.ResolvedCookies,
                            UserAgent = resolver.ResolvedUserAgent
                        };

                        AppLogger.Info($"[AddURL] Resolved via Chromium session: '{info.FileName}' ({FileInfoForm.FormatBytes(info.TotalBytes)})");
                    }
                    else
                    {
                        AppLogger.Warn("[AddURL] Verification window closed without authorization.");
                        lblStatus.Text = "Verification cancelled.";
                        btnStart.Enabled = true;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[AddURL] Probing failed for {uriResult.AbsoluteUri}", ex);

                var dialogResult = MessageBox.Show(
                    $"Unable to probe file details ({ex.Message}).\n\nWould you like to proceed with the download anyway?",
                    "Proceed to Download",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (dialogResult == DialogResult.Yes)
                {
                    string fallbackFileName = Path.GetFileName(uriResult.LocalPath);
                    if (string.IsNullOrWhiteSpace(fallbackFileName)) fallbackFileName = "download.bin";

                    info = new DownloadInfo
                    {
                        Url = uriResult.AbsoluteUri,
                        FileName = fallbackFileName,
                        TotalBytes = -1,
                        Category = CategorizeExtension(Path.GetExtension(fallbackFileName)),
                        SaveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
                    };
                }
                else
                {
                    lblStatus.Text = "Probe aborted.";
                    btnStart.Enabled = true;
                    return;
                }
            }
            finally
            {
                btnStart.Enabled = true;
            }

            if (info != null)
            {
                using var infoDialog = new FileInfoForm(info);
                if (infoDialog.ShowDialog(this) == DialogResult.OK)
                {
                    this.ResultDownloadInfo = infoDialog.FinalInfo;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                }
            }
        }

        private async Task<DownloadInfo> FetchRemoteFileInfoAsync(Uri uri, string? cookies = null, string? userAgent = null)
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                AutomaticDecompression = DecompressionMethods.All,
                UseCookies = false // Crucial: false ensures manual Cookie headers are sent
            };

            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };

            if (!string.IsNullOrWhiteSpace(userAgent))
            {
                client.DefaultRequestHeaders.UserAgent.Clear();
                client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            }
            else
            {
                ApplyBrowserHeaders(client);
            }

            using var probeReq = new HttpRequestMessage(HttpMethod.Get, uri);
            probeReq.Headers.Referrer = GetRefererUri(uri);
            probeReq.Headers.Range = new RangeHeaderValue(0, 0);

            if (!string.IsNullOrWhiteSpace(cookies))
            {
                probeReq.Headers.TryAddWithoutValidation("Cookie", cookies);
            }

            AppLogger.Http($"[Probe Request] GET {uri} | Range: bytes=0-0 | Cookies: {(string.IsNullOrEmpty(cookies) ? "(None)" : "Present")}");
            var res = await client.SendAsync(probeReq, HttpCompletionOption.ResponseHeadersRead);
            AppLogger.Http($"[Probe Response] Status: {(int)res.StatusCode} {res.ReasonPhrase}");

            if ((int)res.StatusCode == 403)
            {
                if (string.IsNullOrEmpty(cookies))
                {
                    throw new CloudflareChallengeException();
                }
                throw new HttpRequestException("Server rejected HttpClient request (403 Forbidden).");
            }

            res.EnsureSuccessStatusCode();

            long totalBytes = -1;
            if (res.Content.Headers.ContentRange?.Length.HasValue == true)
            {
                totalBytes = res.Content.Headers.ContentRange.Length.Value;
                AppLogger.Info($"[Probe Resolved] File size from Content-Range: {totalBytes} bytes");
            }
            else if (res.Content.Headers.ContentLength.HasValue)
            {
                totalBytes = res.Content.Headers.ContentLength.Value;
                AppLogger.Info($"[Probe Resolved] File size from Content-Length: {totalBytes} bytes");
            }

            string fileName = "";
            if (res.Content.Headers.ContentDisposition?.FileName != null)
            {
                fileName = res.Content.Headers.ContentDisposition.FileName.Trim('\"', '\'');
            }
            else
            {
                fileName = Path.GetFileName(uri.LocalPath);
            }

            if (string.IsNullOrWhiteSpace(fileName)) fileName = "downloaded_file.bin";

            string category = CategorizeExtension(Path.GetExtension(fileName));
            string defaultFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

            return new DownloadInfo
            {
                Url = uri.AbsoluteUri,
                FileName = fileName,
                TotalBytes = totalBytes,
                Category = category,
                SaveDirectory = defaultFolder,
                Cookies = cookies,
                UserAgent = userAgent
            };
        }

        public static void ApplyBrowserHeaders(HttpClient client)
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", "*/*");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            client.DefaultRequestHeaders.Add("sec-ch-ua", "\"Chromium\";v=\"124\", \"Google Chrome\";v=\"124\", \"Not-A.Brand\";v=\"99\"");
            client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
            client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");
        }

        public static Uri GetRefererUri(Uri uri)
        {
            string host = uri.Host;
            if (host.StartsWith("files.", StringComparison.OrdinalIgnoreCase))
            {
                host = host.Substring(6);
            }
            return new Uri($"{uri.Scheme}://{host}/");
        }

        public static string CategorizeExtension(string extension)
        {
            string ext = extension.ToLowerInvariant();

            string[] archived = { ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".iso", ".xz", ".cab" };
            if (archived.Contains(ext)) return "Archived";

            string[] media = { ".mp3", ".wav", ".aac", ".flac", ".mp4", ".mkv", ".avi", ".mov", ".flv", ".webm", ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            if (media.Contains(ext)) return "Media";

            string[] applications = { ".exe", ".msi", ".bat", ".cmd", ".apk", ".dmg", ".pkg", ".appimage" };
            if (applications.Contains(ext)) return "Applications";

            string[] documents = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".rtf", ".odt", ".csv" };
            if (documents.Contains(ext)) return "Documents";

            return "Others";
        }
    }

    public class CloudflareChallengeException : Exception { }
}