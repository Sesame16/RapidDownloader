using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Rapid_Downloader
{
    public class DownloadItemState
    {
        public DownloadInfo Info { get; set; } = new();
        public CancellationTokenSource? Cts { get; set; }
    }

    public partial class Form1 : Form
    {
        private readonly PrivateFontCollection _fontCollection = new PrivateFontCollection();
        private FontFamily? _iconFontFamily;
        private ToolStrip? toolStripIcons;
        private ListViewGridDrawer? _gridDrawer;

        private readonly List<ListViewItem> _allDownloadItems = new();
        private string _currentCategoryFilter = "All Downloads";

        // Context Menu Controls (Open File With removed)
        private ContextMenuStrip contextMenuDownloads = null!;
        private ToolStripMenuItem menuOpenFile = null!;
        private ToolStripMenuItem menuOpenFolder = null!;
        private ToolStripMenuItem menuResume = null!;
        private ToolStripMenuItem menuStop = null!;
        private ToolStripMenuItem menuRefresh = null!;
        private ToolStripMenuItem menuCopyUrl = null!;
        private ToolStripMenuItem menuDelete = null!;
        private ToolStripMenuItem menuProperties = null!;

        // Win32 API for native Windows Properties dialog
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHELLEXECUTEINFO
        {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpVerb;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpFile;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpParameters;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpDirectory;
            public int nShow;
            public IntPtr hInstApp;
            public IntPtr lpIDList;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpClass;
            public IntPtr hkeyClass;
            public uint dwHotKey;
            public IntPtr hIcon;
            public IntPtr hProcess;
        }

        private const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object? sender, EventArgs e)
        {
            AppLogger.Info("[App] Rapid Downloader initializing...");

            ApplyApplicationIcon();
            LoadRemixIconFont();
            InitializeIconToolbar();
            InitializeCategoriesTree();
            InitializeTaskListView();
            InitializeContextMenu();

            _gridDrawer = new ListViewGridDrawer(taskListView);

            AppLogger.Info("[App] Main window ready and listening for tasks.");
        }

        private void ApplyApplicationIcon()
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fonts", "glyphs-poly--rocket.ico");
            if (!File.Exists(iconPath))
            {
                iconPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\fonts\glyphs-poly--rocket.ico"));
            }

            if (File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
            }
        }

        private void LoadRemixIconFont()
        {
            string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fonts", "remixicon.ttf");
            if (!File.Exists(fontPath))
            {
                fontPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\fonts\remixicon.ttf"));
            }

            if (File.Exists(fontPath))
            {
                _fontCollection.AddFontFile(fontPath);
                if (_fontCollection.Families.Length > 0)
                {
                    _iconFontFamily = _fontCollection.Families[0];
                }
            }
        }

        private void InitializeIconToolbar()
        {
            toolStripIcons = new ToolStrip
            {
                Dock = DockStyle.Top,
                ImageScalingSize = new Size(24, 24),
                GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding(8, 4, 8, 4),
                BackColor = SystemColors.Control
            };

            AddIconButton("Add URL", "\uEA13", Color.FromArgb(0, 120, 215), BtnAdd_Click);
            AddIconButton("Resume", "\uF00A", Color.FromArgb(16, 124, 65), BtnResume_Click);
            AddIconButton("Stop", "\uF1A1", Color.FromArgb(209, 52, 56), BtnStop_Click);
            AddIconButton("Stop All", "\uEE9A", Color.FromArgb(180, 40, 40), BtnStopAll_Click);
            AddIconButton("Delete", "\uEC24", Color.FromArgb(100, 100, 100), BtnDelete_Click);

            toolStripIcons.Items.Add(new ToolStripSeparator());

            AddIconButton("Options", "\uF0E6", Color.FromArgb(70, 70, 70), BtnOptions_Click);
            AddIconButton("About", "\uEE58", Color.FromArgb(0, 102, 204), BtnAbout_Click);
            AddIconButton("Logs", "\uECEB", Color.FromArgb(80, 80, 80), BtnLogs_Click);

            this.Controls.Add(toolStripIcons);
            toolStripIcons.SendToBack();
        }

        private void InitializeContextMenu()
        {
            contextMenuDownloads = new ContextMenuStrip();

            menuOpenFile = new ToolStripMenuItem("Open File", null, Context_OpenFile_Click);
            menuOpenFolder = new ToolStripMenuItem("Open Containing Folder", null, Context_OpenFolder_Click);

            menuResume = new ToolStripMenuItem("Resume Download", null, BtnResume_Click);
            menuStop = new ToolStripMenuItem("Stop Download", null, BtnStop_Click);
            menuRefresh = new ToolStripMenuItem("Refresh Download", null, Context_Refresh_Click);

            menuCopyUrl = new ToolStripMenuItem("Copy Download URL", null, Context_CopyUrl_Click);
            menuDelete = new ToolStripMenuItem("Delete", null, BtnDelete_Click);
            menuProperties = new ToolStripMenuItem("Properties", null, Context_Properties_Click);

            // Cleanly organized context menu without 'Open File With'
            contextMenuDownloads.Items.AddRange(new ToolStripItem[]
            {
                menuOpenFile,
                menuOpenFolder,
                new ToolStripSeparator(),
                menuResume,
                menuStop,
                menuRefresh,
                new ToolStripSeparator(),
                menuCopyUrl,
                new ToolStripSeparator(),
                menuDelete,
                new ToolStripSeparator(),
                menuProperties
            });

            contextMenuDownloads.Opening += ContextMenuDownloads_Opening;
            taskListView.ContextMenuStrip = contextMenuDownloads;
        }

        private void ContextMenuDownloads_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (taskListView.SelectedItems.Count == 0)
            {
                e.Cancel = true;
                return;
            }

            var selectedItem = taskListView.SelectedItems[0];
            string status = selectedItem.SubItems[2].Text;
            bool isCompleted = status == "Completed";
            bool isRunning = status.Contains("%") || status == "Downloading..." || status == "Connecting..." || status == "Resuming...";

            menuOpenFile.Enabled = isCompleted;
            menuOpenFolder.Enabled = true;

            menuResume.Enabled = !isRunning && !isCompleted;
            menuStop.Enabled = isRunning;
            menuRefresh.Enabled = true;

            menuCopyUrl.Enabled = true;
            menuDelete.Enabled = true;
            menuProperties.Enabled = true;
        }

        private void Context_OpenFile_Click(object? sender, EventArgs e)
        {
            if (taskListView.SelectedItems.Count == 0) return;
            if (taskListView.SelectedItems[0].Tag is not DownloadItemState state) return;

            AppLogger.Info($"[File Action] Opening completed file: '{state.Info.FullSavePath}'");

            if (File.Exists(state.Info.FullSavePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = state.Info.FullSavePath,
                    UseShellExecute = true
                });
            }
            else
            {
                AppLogger.Warn($"[File Action] Open failed. File not found on disk: '{state.Info.FullSavePath}'");
                MessageBox.Show("File was not found on disk.", "File Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Context_OpenFolder_Click(object? sender, EventArgs e)
        {
            if (taskListView.SelectedItems.Count == 0) return;
            if (taskListView.SelectedItems[0].Tag is not DownloadItemState state) return;

            AppLogger.Info($"[File Action] Opening containing directory for: '{state.Info.FullSavePath}'");

            if (File.Exists(state.Info.FullSavePath))
            {
                Process.Start("explorer.exe", $"/select,\"{state.Info.FullSavePath}\"");
            }
            else if (Directory.Exists(state.Info.SaveDirectory))
            {
                Process.Start("explorer.exe", $"\"{state.Info.SaveDirectory}\"");
            }
        }

        private void Context_Refresh_Click(object? sender, EventArgs e)
        {
            if (taskListView.SelectedItems.Count == 0) return;
            var selectedItem = taskListView.SelectedItems[0];
            if (selectedItem.Tag is not DownloadItemState state) return;

            AppLogger.Info($"[Download Action] Refresh requested for '{state.Info.FileName}'");

            var confirm = MessageBox.Show(
                $"Restart download for '{state.Info.FileName}' from 0%?",
                "Refresh Download",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
            {
                AppLogger.Info($"[Download Action] Refresh cancelled for '{state.Info.FileName}'");
                return;
            }

            state.Cts?.Cancel();

            try
            {
                if (File.Exists(state.Info.FullSavePath))
                {
                    File.Delete(state.Info.FullSavePath);
                    AppLogger.Info($"[Download Action] Truncated/deleted previous file on disk: '{state.Info.FullSavePath}'");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"[Download Action] Could not delete old file prior to refresh: {ex.Message}");
            }

            selectedItem.SubItems[2].Text = "Restarting...";
            selectedItem.SubItems[3].Text = "--:--";
            selectedItem.SubItems[4].Text = "0 KB/s";

            LaunchWorker(selectedItem, state);
        }

        private void Context_CopyUrl_Click(object? sender, EventArgs e)
        {
            if (taskListView.SelectedItems.Count == 0) return;
            if (taskListView.SelectedItems[0].Tag is not DownloadItemState state) return;

            Clipboard.SetText(state.Info.Url);
            AppLogger.Info($"[Clipboard] Download URL copied: '{state.Info.Url}'");
        }

        private void Context_Properties_Click(object? sender, EventArgs e)
        {
            if (taskListView.SelectedItems.Count == 0) return;
            if (taskListView.SelectedItems[0].Tag is not DownloadItemState state) return;

            AppLogger.Info($"[Shell Action] Properties dialog requested for '{state.Info.FileName}'");

            if (!File.Exists(state.Info.FullSavePath))
            {
                MessageBox.Show(
                    $"File: {state.Info.FileName}\nCategory: {state.Info.Category}\nTarget Path: {state.Info.FullSavePath}\n\n(Windows properties dialog requires the file to be present on disk).",
                    "Download Details",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            SHELLEXECUTEINFO info = new SHELLEXECUTEINFO();
            info.cbSize = Marshal.SizeOf(info);
            info.lpVerb = "properties";
            info.lpFile = state.Info.FullSavePath;
            info.nShow = 5;
            info.fMask = SEE_MASK_INVOKEIDLIST;
            info.hwnd = this.Handle;

            ShellExecuteEx(ref info);
        }

        private void InitializeCategoriesTree()
        {
            treeViewCategories.Nodes.Clear();

            TreeNode rootNode = new TreeNode("All Downloads");
            rootNode.Nodes.Add("Documents");
            rootNode.Nodes.Add("Media");
            rootNode.Nodes.Add("Applications");
            rootNode.Nodes.Add("Archived");
            rootNode.Nodes.Add("Others");

            treeViewCategories.Nodes.Add(rootNode);
            rootNode.Expand();

            treeViewCategories.AfterSelect -= TreeViewCategories_AfterSelect;
            treeViewCategories.AfterSelect += TreeViewCategories_AfterSelect;

            treeViewCategories.SelectedNode = rootNode;
        }

        private void TreeViewCategories_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            if (e.Node == null) return;
            _currentCategoryFilter = e.Node.Text;
            AppLogger.Info($"[Categories] Filter switched to: '{_currentCategoryFilter}'");
            ApplyCategoryFilter();
        }

        private void ApplyCategoryFilter()
        {
            taskListView.BeginUpdate();
            taskListView.Items.Clear();

            foreach (var item in _allDownloadItems)
            {
                if (item.Tag is DownloadItemState state)
                {
                    if (_currentCategoryFilter == "All Downloads" ||
                        string.Equals(state.Info.Category, _currentCategoryFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        taskListView.Items.Add(item);
                    }
                }
            }

            taskListView.EndUpdate();
        }

        private void InitializeTaskListView()
        {
            taskListView.View = View.Details;
            taskListView.FullRowSelect = true;
            taskListView.GridLines = true;

            taskListView.Columns.Clear();
            taskListView.Columns.Add("File Name", 200);
            taskListView.Columns.Add("Size", 85);
            taskListView.Columns.Add("Status", 100);
            taskListView.Columns.Add("ETA", 80);
            taskListView.Columns.Add("Speed Rate", 95);
            taskListView.Columns.Add("Added Date", 125);

            taskListView.DoubleClick += (s, e) => Context_OpenFile_Click(s, e);
            taskListView.Items.Clear();
        }

        private void AddIconButton(string text, string unicodeChar, Color iconColor, EventHandler? onClick)
        {
            var btn = new ToolStripButton
            {
                Text = text,
                Image = CreateFontIcon(unicodeChar, 24, iconColor),
                TextImageRelation = TextImageRelation.ImageAboveText,
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                Margin = new Padding(4, 0, 4, 0),
                Font = new Font(this.Font.FontFamily, 8.25f, FontStyle.Regular)
            };

            if (onClick != null)
            {
                btn.Click += onClick;
            }

            toolStripIcons?.Items.Add(btn);
        }

        private Image CreateFontIcon(string unicodeGlyph, int iconSize, Color iconColor)
        {
            Bitmap bmp = new Bitmap(iconSize, iconSize);
            if (_iconFontFamily == null) return bmp;

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                using (Font font = new Font(_iconFontFamily, iconSize * 0.75f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (Brush brush = new SolidBrush(iconColor))
                {
                    StringFormat sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };

                    g.DrawString(unicodeGlyph, font, brush, new RectangleF(0, 0, iconSize, iconSize), sf);
                }
            }

            return bmp;
        }

        private void BtnAdd_Click(object? sender, EventArgs e)
        {
            AppLogger.Info("[Toolbar] Add URL button clicked.");
            using var addUrlDlg = new AddUrlForm();
            if (addUrlDlg.ShowDialog(this) == DialogResult.OK && addUrlDlg.ResultDownloadInfo != null)
            {
                StartDownload(addUrlDlg.ResultDownloadInfo);
            }
        }

        private void StartDownload(DownloadInfo info)
        {
            AppLogger.Info($"[Task Added] File: '{info.FileName}' | Size: {FileInfoForm.FormatBytes(info.TotalBytes)} | Category: '{info.Category}' | Location: '{info.FullSavePath}'");

            var item = new ListViewItem(info.FileName);
            item.SubItems.Add(FileInfoForm.FormatBytes(info.TotalBytes));
            item.SubItems.Add("Connecting...");
            item.SubItems.Add("--:--");
            item.SubItems.Add("0 KB/s");
            item.SubItems.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));

            var state = new DownloadItemState { Info = info };
            item.Tag = state;

            _allDownloadItems.Add(item);
            ApplyCategoryFilter();

            LaunchWorker(item, state);
        }

        private void LaunchWorker(ListViewItem item, DownloadItemState state)
        {
            state.Cts = new CancellationTokenSource();
            CancellationToken token = state.Cts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    Directory.CreateDirectory(state.Info.SaveDirectory);
                    string destinationPath = state.Info.FullSavePath;

                    long existingBytes = 0;
                    if (File.Exists(destinationPath))
                    {
                        existingBytes = new FileInfo(destinationPath).Length;
                    }

                    // If already complete, finalize immediately
                    if (state.Info.TotalBytes > 0 && existingBytes == state.Info.TotalBytes)
                    {
                        AppLogger.Info($"[Download Verified] '{state.Info.FileName}' is already complete.");
                        this.BeginInvoke(new Action(() =>
                        {
                            item.SubItems[2].Text = "Completed";
                            item.SubItems[3].Text = "00:00";
                            item.SubItems[4].Text = "--";
                        }));
                        return;
                    }

                    // If existing file is larger than the total size or corrupted, truncate and restart
                    if (state.Info.TotalBytes > 0 && existingBytes > state.Info.TotalBytes)
                    {
                        AppLogger.Warn($"[Download Stream] Local file ({existingBytes} bytes) exceeds total size ({state.Info.TotalBytes} bytes). Resetting to 0.");
                        existingBytes = 0;
                        try { File.Delete(destinationPath); } catch { }
                    }

                    var handler = new HttpClientHandler
                    {
                        AllowAutoRedirect = true,
                        AutomaticDecompression = DecompressionMethods.All,
                        UseCookies = false // Ensures Cookie header is sent over the wire
                    };

                    using var client = new HttpClient(handler);

                    if (!string.IsNullOrWhiteSpace(state.Info.UserAgent))
                    {
                        client.DefaultRequestHeaders.UserAgent.Clear();
                        client.DefaultRequestHeaders.UserAgent.ParseAdd(state.Info.UserAgent);
                    }
                    else
                    {
                        AddUrlForm.ApplyBrowserHeaders(client);
                    }

                    using var request = new HttpRequestMessage(HttpMethod.Get, state.Info.Url);
                    if (Uri.TryCreate(state.Info.Url, UriKind.Absolute, out Uri? parsedUri) && parsedUri != null)
                    {
                        request.Headers.Referrer = AddUrlForm.GetRefererUri(parsedUri);
                    }

                    if (!string.IsNullOrWhiteSpace(state.Info.Cookies))
                    {
                        request.Headers.TryAddWithoutValidation("Cookie", state.Info.Cookies);
                    }

                    if (existingBytes > 0)
                    {
                        request.Headers.Range = new RangeHeaderValue(existingBytes, null);
                        AppLogger.Info($"[Download Stream] Requesting resume for '{state.Info.FileName}' from byte {existingBytes}");
                    }
                    else
                    {
                        AppLogger.Info($"[Download Stream] Starting download for '{state.Info.FileName}' from origin {state.Info.Url}");
                    }

                    var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

                    // Handle invalid range rejection (e.g. 416 or 403 on bad Range): retry from byte 0
                    if (existingBytes > 0 && ((int)response.StatusCode == 416 || (int)response.StatusCode == 403))
                    {
                        AppLogger.Warn($"[Download Stream] Server rejected range request ({response.StatusCode}). Retrying fresh from byte 0...");
                        response.Dispose();
                        existingBytes = 0;
                        try { File.Delete(destinationPath); } catch { }

                        using var retryReq = new HttpRequestMessage(HttpMethod.Get, state.Info.Url);
                        if (parsedUri != null) retryReq.Headers.Referrer = AddUrlForm.GetRefererUri(parsedUri);
                        if (!string.IsNullOrWhiteSpace(state.Info.Cookies)) retryReq.Headers.TryAddWithoutValidation("Cookie", state.Info.Cookies);

                        response = await client.SendAsync(retryReq, HttpCompletionOption.ResponseHeadersRead, token);
                    }

                    response.EnsureSuccessStatusCode();

                    bool appendMode = (response.StatusCode == HttpStatusCode.PartialContent);
                    if (!appendMode) existingBytes = 0;

                    long? contentLen = response.Content.Headers.ContentLength;
                    long totalBytes = appendMode ? (existingBytes + (contentLen ?? 0)) : (contentLen ?? state.Info.TotalBytes);

                    if (state.Info.TotalBytes <= 0 && totalBytes > 0)
                    {
                        state.Info.TotalBytes = totalBytes;
                        this.BeginInvoke(new Action(() =>
                        {
                            item.SubItems[1].Text = FileInfoForm.FormatBytes(totalBytes);
                        }));
                        AppLogger.Info($"[Download Stream] Size resolved: {FileInfoForm.FormatBytes(totalBytes)}");
                    }

                    using var stream = await response.Content.ReadAsStreamAsync(token);
                    using var fs = new FileStream(destinationPath, appendMode ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None);

                    byte[] buffer = new byte[81920];
                    long totalRead = existingBytes;
                    int bytesRead;

                    var stopwatch = Stopwatch.StartNew();
                    long lastBytes = totalRead;
                    var lastTime = stopwatch.ElapsedMilliseconds;

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                    {
                        await fs.WriteAsync(buffer, 0, bytesRead, token);
                        totalRead += bytesRead;

                        long now = stopwatch.ElapsedMilliseconds;
                        if (now - lastTime >= 500)
                        {
                            double intervalSec = (now - lastTime) / 1000.0;
                            double speedBytesPerSec = (totalRead - lastBytes) / intervalSec;

                            lastBytes = totalRead;
                            lastTime = now;

                            int percent = totalBytes > 0 ? (int)((totalRead * 100) / totalBytes) : 0;
                            string etaStr = "--:--";
                            if (totalBytes > 0 && speedBytesPerSec > 0)
                            {
                                long remaining = totalBytes - totalRead;
                                var etaSpan = TimeSpan.FromSeconds(remaining / speedBytesPerSec);
                                etaStr = etaSpan.TotalHours >= 1 ? etaSpan.ToString(@"hh\:mm\:ss") : etaSpan.ToString(@"mm\:ss");
                            }

                            string speedStr = $"{FileInfoForm.FormatBytes((long)speedBytesPerSec)}/s";

                            this.BeginInvoke(new Action(() =>
                            {
                                item.SubItems[2].Text = totalBytes > 0 ? $"{percent}%" : "Downloading...";
                                item.SubItems[3].Text = etaStr;
                                item.SubItems[4].Text = speedStr;
                            }));
                        }
                    }

                    AppLogger.Info($"[Download Finished] '{state.Info.FileName}' completed -> '{destinationPath}'");

                    this.BeginInvoke(new Action(() =>
                    {
                        item.SubItems[2].Text = "Completed";
                        item.SubItems[3].Text = "00:00";
                        item.SubItems[4].Text = "--";
                    }));
                }
                catch (OperationCanceledException)
                {
                    AppLogger.Info($"[Download Paused] '{state.Info.FileName}' paused.");
                    this.BeginInvoke(new Action(() =>
                    {
                        item.SubItems[2].Text = "Stopped";
                        item.SubItems[3].Text = "--:--";
                        item.SubItems[4].Text = "0 KB/s";
                    }));
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"[Download Failed] '{state.Info.FileName}' error", ex);
                    this.BeginInvoke(new Action(() =>
                    {
                        item.SubItems[2].Text = "Error";
                        item.SubItems[3].Text = "--:--";
                        item.SubItems[4].Text = "--";
                    }));
                    MessageBox.Show($"Download failed: {ex.Message}", "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });
        }

        private void BtnResume_Click(object? sender, EventArgs e)
        {
            if (taskListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a download task to resume.", "Resume Download", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedItem = taskListView.SelectedItems[0];
            if (selectedItem.Tag is DownloadItemState state)
            {
                string status = selectedItem.SubItems[2].Text;
                if (status == "Completed")
                {
                    MessageBox.Show("This download is already completed.", "Resume", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (status.Contains("%") || status == "Downloading..." || status == "Connecting...")
                {
                    return;
                }

                AppLogger.Info($"[Download Action] Resuming task: '{state.Info.FileName}'");
                selectedItem.SubItems[2].Text = "Resuming...";
                LaunchWorker(selectedItem, state);
            }
        }

        private void BtnStop_Click(object? sender, EventArgs e)
        {
            if (taskListView.SelectedItems.Count == 0) return;

            var selectedItem = taskListView.SelectedItems[0];
            if (selectedItem.Tag is DownloadItemState state)
            {
                AppLogger.Info($"[Download Action] Stopping task: '{state.Info.FileName}'");
                state.Cts?.Cancel();
            }
        }

        private void BtnStopAll_Click(object? sender, EventArgs e)
        {
            AppLogger.Info($"[Download Action] Stop All triggered across {_allDownloadItems.Count} total task(s).");
            foreach (var item in _allDownloadItems)
            {
                if (item.Tag is DownloadItemState state)
                {
                    state.Cts?.Cancel();
                }
            }
        }

        private void BtnAbout_Click(object? sender, EventArgs e)
        {
            AppLogger.Info("[UI Action] About window opened.");
            using var aboutDlg = new AboutForm(this.Icon);
            aboutDlg.ShowDialog(this);
        }

        private void BtnLogs_Click(object? sender, EventArgs e)
        {
            AppLogger.Info("[UI Action] Logs window opened.");
            var logsForm = new LogsForm(this.Icon);
            logsForm.Show(this);
        }

        // Unified Delete Action: Used by both Toolbar 'Delete' and Context Menu 'Delete'
        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            HandleDeleteTask();
        }

        private void HandleDeleteTask()
        {
            if (taskListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a download item to delete.", "Delete Download", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedItem = taskListView.SelectedItems[0];
            if (selectedItem.Tag is not DownloadItemState state) return;

            using var confirmDlg = new DeleteConfirmForm(state.Info.FileName, this.Icon);
            if (confirmDlg.ShowDialog(this) == DialogResult.OK)
            {
                state.Cts?.Cancel();

                if (confirmDlg.UserChoice == DeleteActionChoice.DeletePermanently)
                {
                    try
                    {
                        if (File.Exists(state.Info.FullSavePath))
                        {
                            File.Delete(state.Info.FullSavePath);
                            AppLogger.Info($"[Delete Permanently] Deleted file from disk: '{state.Info.FullSavePath}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"[Delete Permanently] Failed to remove file from disk: '{state.Info.FullSavePath}'", ex);
                        MessageBox.Show($"Could not delete file from disk: {ex.Message}", "Delete Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }

                    AppLogger.Info($"[Delete Permanently] Removed '{state.Info.FileName}' from download list and disk.");
                }
                else if (confirmDlg.UserChoice == DeleteActionChoice.RemoveFromListOnly)
                {
                    AppLogger.Info($"[Delete List Only] Removed '{state.Info.FileName}' from download list. File kept on disk: '{state.Info.FullSavePath}'");
                }

                _allDownloadItems.Remove(selectedItem);
                taskListView.Items.Remove(selectedItem);
            }
            else
            {
                AppLogger.Info($"[Delete Action] Deletion cancelled for '{state.Info.FileName}'");
            }
        }

        private void BtnOptions_Click(object? sender, EventArgs e)
        {
            AppLogger.Info("[UI Action] Options dialog clicked.");
            MessageBox.Show("Configuration dialog triggered", "Options");
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            AppLogger.Info("[App] Rapid Downloader closing. Cancelling all workers and releasing resources...");
            BtnStopAll_Click(null, EventArgs.Empty);
            _gridDrawer?.ReleaseHandle();
            _fontCollection.Dispose();
            base.OnFormClosed(e);
        }

        private class ListViewGridDrawer : NativeWindow
        {
            private readonly ListView _listView;

            public ListViewGridDrawer(ListView listView)
            {
                _listView = listView;
                AssignHandle(listView.Handle);
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                const int WM_PAINT = 0x000F;
                if (m.Msg == WM_PAINT && _listView.IsHandleCreated && _listView.Visible && _listView.View == View.Details)
                {
                    using var g = Graphics.FromHwnd(_listView.Handle);
                    using var pen = new Pen(Color.FromArgb(240, 240, 240));

                    int clientHeight = _listView.ClientSize.Height;
                    int clientWidth = _listView.ClientSize.Width;

                    int currentX = 0;
                    for (int i = 0; i < _listView.Columns.Count; i++)
                    {
                        currentX += _listView.Columns[i].Width;
                        g.DrawLine(pen, currentX - 1, 0, currentX - 1, clientHeight);
                    }

                    if (_listView.Items.Count == 0)
                    {
                        int rowHeight = 22;
                        for (int y = rowHeight; y < clientHeight; y += rowHeight)
                        {
                            g.DrawLine(pen, 0, y, clientWidth, y);
                        }
                    }
                }
            }
        }
    }
}