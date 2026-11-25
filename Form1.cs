using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Rapid_Downloader
{
    public class DownloadItemState
    {
        public DownloadInfo Info { get; set; } = new();
        public CoreWebView2DownloadOperation? DownloadOperation { get; set; }
        public CancellationTokenSource? Cts { get; set; }
        public int Percent { get; set; } = 0;
    }

    public partial class Form1 : Form
    {
        private readonly PrivateFontCollection _fontCollection = new PrivateFontCollection();
        private FontFamily? _iconFontFamily;
        private ToolStrip? toolStripIcons;
        private ListViewGridDrawer? _gridDrawer;

        private WebView2 _downloadEngine = null!;
        private readonly Dictionary<string, (ListViewItem Item, DownloadItemState State)> _pendingTasks = new();

        private readonly List<ListViewItem> _allDownloadItems = new();
        private string _currentCategoryFilter = "All Downloads";

        private ContextMenuStrip contextMenuDownloads = null!;
        private ToolStripMenuItem menuOpenFile = null!;
        private ToolStripMenuItem menuOpenFolder = null!;
        private ToolStripMenuItem menuResume = null!;
        private ToolStripMenuItem menuStop = null!;
        private ToolStripMenuItem menuRefresh = null!;
        private ToolStripMenuItem menuCopyUrl = null!;
        private ToolStripMenuItem menuDelete = null!;
        private ToolStripMenuItem menuProperties = null!;

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

        private async void Form1_Load(object? sender, EventArgs e)
        {
            AppLogger.Info("[App] Rapid Downloader initializing...");

            ApplyApplicationIcon();
            LoadRemixIconFont();
            InitializeIconToolbar();
            InitializeCategoriesTree();
            InitializeTaskListView();
            InitializeContextMenu();

            _gridDrawer = new ListViewGridDrawer(taskListView);

            await InitializeBackgroundDownloadEngineAsync();

            AppLogger.Info("[App] Main window ready and listening for tasks.");
        }

        private async Task InitializeBackgroundDownloadEngineAsync()
        {
            try
            {
                _downloadEngine = new WebView2
                {
                    Size = new Size(200, 200),
                    Location = new Point(-3500, -3500),
                    Visible = true
                };
                this.Controls.Add(_downloadEngine);

                var env = await WebView2Helper.GetEnvironmentAsync();
                await _downloadEngine.EnsureCoreWebView2Async(env);

                _downloadEngine.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;

                _downloadEngine.CoreWebView2.NavigationStarting += (s, args) =>
                {
                    AppLogger.Info($"[WebView2 Engine] Navigation started: {args.Uri}");
                };

                _downloadEngine.CoreWebView2.NavigationCompleted += async (s, args) =>
                {
                    AppLogger.Info($"[WebView2 Engine] Navigation completed (Success={args.IsSuccess}, Status={args.HttpStatusCode})");

                    // If the URL loaded as an inline document (PDF/Image) rather than triggering DownloadStarting,
                    // trigger an anchor click to force Chromium's DownloadManager to take over.
                    if (args.IsSuccess && (args.HttpStatusCode == 200 || args.HttpStatusCode == 0))
                    {
                        DownloadItemState? pendingState = null;
                        lock (_pendingTasks)
                        {
                            if (_pendingTasks.Count > 0)
                            {
                                pendingState = _pendingTasks.Values.FirstOrDefault().State;
                            }
                        }

                        if (pendingState != null && pendingState.DownloadOperation == null)
                        {
                            AppLogger.Info($"[WebView2 Engine] Document loaded inline. Triggering download handler for '{pendingState.Info.FileName}'...");

                            string script = $@"
                        (() => {{
                            try {{
                                let a = document.createElement('a');
                                a.href = window.location.href;
                                a.setAttribute('download', '{pendingState.Info.FileName}');
                                document.body.appendChild(a);
                                a.click();
                                a.remove();
                            }} catch(e) {{}}
                        }})();";

                            await _downloadEngine.CoreWebView2.ExecuteScriptAsync(script);
                        }
                    }
                };

                AppLogger.Info("[WebView2 Engine] Shared background download engine ready.");
            }
            catch (Exception ex)
            {
                AppLogger.Error("[WebView2 Engine] Background engine initialization failed", ex);
            }
        }

        private void CoreWebView2_DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
        {
            e.Handled = true; // Suppress Edge's default UI

            string uri = e.DownloadOperation.Uri;
            AppLogger.Info($"[WebView2 Download] Intercepted stream for: {uri}");

            ListViewItem? item = null;
            DownloadItemState? state = null;

            lock (_pendingTasks)
            {
                // 1. Direct match
                if (_pendingTasks.TryGetValue(uri, out var match))
                {
                    item = match.Item;
                    state = match.State;
                    _pendingTasks.Remove(uri);
                }
                else
                {
                    // 2. Redirect / substring match
                    var key = _pendingTasks.Keys.FirstOrDefault(k =>
                        uri.StartsWith(k, StringComparison.OrdinalIgnoreCase) ||
                        k.StartsWith(uri, StringComparison.OrdinalIgnoreCase) ||
                        uri.Contains(Path.GetFileNameWithoutExtension(k), StringComparison.OrdinalIgnoreCase));

                    if (key != null)
                    {
                        var fallbackMatch = _pendingTasks[key];
                        item = fallbackMatch.Item;
                        state = fallbackMatch.State;
                        _pendingTasks.Remove(key);
                    }
                    else if (_pendingTasks.Count > 0)
                    {
                        // 3. FIFO fallback for single active downloads
                        var firstKey = _pendingTasks.Keys.First();
                        var fallbackMatch = _pendingTasks[firstKey];
                        item = fallbackMatch.Item;
                        state = fallbackMatch.State;
                        _pendingTasks.Remove(firstKey);
                    }
                }
            }

            if (item == null || state == null)
            {
                AppLogger.Warn($"[WebView2 Download] No corresponding UI item found for: {uri}");
                return;
            }

            e.ResultFilePath = state.Info.FullSavePath;
            state.DownloadOperation = e.DownloadOperation;

            AppLogger.Info($"[WebView2 Download] Saving '{state.Info.FileName}' to '{e.ResultFilePath}'");

            var download = e.DownloadOperation;
            var stopwatch = Stopwatch.StartNew();
            long lastBytes = 0;
            long lastTime = 0;

            download.BytesReceivedChanged += (s, args) =>
            {
                long received = (long)download.BytesReceived;
                long? total = (long?)download.TotalBytesToReceive;

                if (state.Info.TotalBytes <= 0 && total.HasValue && total.Value > 0)
                {
                    state.Info.TotalBytes = total.Value;
                    this.BeginInvoke(new Action(() =>
                    {
                        item.SubItems[1].Text = FileInfoForm.FormatBytes(total.Value);
                    }));
                }

                long now = stopwatch.ElapsedMilliseconds;
                if (now - lastTime >= 500)
                {
                    double intervalSec = (now - lastTime) / 1000.0;
                    double speedBytesPerSec = (received - lastBytes) / intervalSec;

                    lastBytes = received;
                    lastTime = now;

                    int percent = total.HasValue && total.Value > 0
                        ? (int)((received * 100) / total.Value)
                        : 0;

                    state.Percent = percent;

                    string etaStr = "--:--";
                    if (total.HasValue && total.Value > 0 && speedBytesPerSec > 0)
                    {
                        long remaining = total.Value - received;
                        var eta = TimeSpan.FromSeconds(remaining / speedBytesPerSec);
                        etaStr = eta.TotalHours >= 1 ? eta.ToString(@"hh\:mm\:ss") : eta.ToString(@"mm\:ss");
                    }

                    string speedStr = $"{FileInfoForm.FormatBytes((long)speedBytesPerSec)}/s";

                    this.BeginInvoke(new Action(() =>
                    {
                        item.SubItems[2].Text = total.HasValue ? $"{percent}%" : "Downloading...";
                        item.SubItems[3].Text = etaStr;
                        item.SubItems[4].Text = speedStr;
                    }));
                }
            };

            download.StateChanged += (s, args) =>
            {
                switch (download.State)
                {
                    case CoreWebView2DownloadState.Completed:
                        state.Percent = 100;
                        AppLogger.Info($"[WebView2 Download] '{state.Info.FileName}' completed successfully.");
                        this.BeginInvoke(new Action(() =>
                        {
                            item.SubItems[2].Text = "Completed";
                            item.SubItems[3].Text = "00:00";
                            item.SubItems[4].Text = "--";
                        }));
                        break;

                    case CoreWebView2DownloadState.Interrupted:
                        AppLogger.Warn($"[WebView2 Download] '{state.Info.FileName}' interrupted. Reason: {download.InterruptReason}");
                        this.BeginInvoke(new Action(() =>
                        {
                            item.SubItems[2].Text = "Stopped";
                            item.SubItems[3].Text = "--:--";
                            item.SubItems[4].Text = "0 KB/s";
                        }));
                        break;
                }
            };
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

            if (confirm != DialogResult.Yes) return;

            state.DownloadOperation?.Cancel();
            state.Cts?.Cancel();
            state.Percent = 0;

            try
            {
                if (File.Exists(state.Info.FullSavePath))
                {
                    File.Delete(state.Info.FullSavePath);
                }
            }
            catch { }

            selectedItem.SubItems[2].Text = "Restarting...";
            selectedItem.SubItems[3].Text = "--:--";
            selectedItem.SubItems[4].Text = "0 KB/s";

            if (string.IsNullOrWhiteSpace(state.Info.Cookies))
                LaunchSegmentedWorker(selectedItem, state);
            else
                LaunchWebView2Download(selectedItem, state);
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

            // Populate child category nodes dynamically from user settings
            foreach (var cat in AppSettings.Current.Categories)
            {
                if (cat.Name != "All Downloads")
                {
                    rootNode.Nodes.Add(cat.Name);
                }
            }

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

            taskListView.OwnerDraw = true;

            typeof(ListView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.SetProperty,
                null, taskListView, new object[] { true });

            taskListView.DrawColumnHeader += TaskListView_DrawColumnHeader;
            taskListView.DrawItem += (s, e) => { };
            taskListView.DrawSubItem += TaskListView_DrawSubItem;

            taskListView.Columns.Clear();
            taskListView.Columns.Add("File Name", 220);
            taskListView.Columns.Add("Size", 85);
            taskListView.Columns.Add("Status", 130);
            taskListView.Columns.Add("ETA", 75);
            taskListView.Columns.Add("Speed Rate", 95);
            taskListView.Columns.Add("Added Date", 125);

            taskListView.DoubleClick += (s, e) => Context_OpenFile_Click(s, e);
            taskListView.Items.Clear();
        }

        private void TaskListView_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void TaskListView_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
        {
            if (e.ColumnIndex != 2)
            {
                e.DrawDefault = true;
                return;
            }

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool isSelected = e.Item?.Selected ?? false;
            Color cellBg = isSelected ? SystemColors.Highlight : Color.White;
            using (SolidBrush bgBrush = new SolidBrush(cellBg))
            {
                g.FillRectangle(bgBrush, e.Bounds);
            }

            Rectangle barRect = new Rectangle(
                e.Bounds.X + 6,
                e.Bounds.Y + 4,
                e.Bounds.Width - 12,
                e.Bounds.Height - 8
            );

            if (barRect.Width <= 0 || barRect.Height <= 0) return;

            string statusText = e.SubItem?.Text ?? string.Empty;
            int percent = 0;
            if (e.Item?.Tag is DownloadItemState state)
            {
                percent = state.Percent;
            }

            Color fillColor = Color.FromArgb(0, 120, 215);
            if (statusText == "Completed")
            {
                fillColor = Color.FromArgb(46, 160, 67);
                percent = 100;
            }
            else if (statusText == "Stopped")
            {
                fillColor = Color.FromArgb(130, 140, 150);
            }
            else if (statusText == "Error")
            {
                fillColor = Color.FromArgb(220, 53, 69);
            }

            using (SolidBrush trackBrush = new SolidBrush(Color.FromArgb(238, 240, 243)))
            {
                using (Pen borderPen = new Pen(Color.FromArgb(205, 210, 218)))
                {
                    g.FillRectangle(trackBrush, barRect);

                    string label = statusText;
                    if (percent > 0 && percent < 100 && statusText != "Stopped" && statusText != "Error")
                    {
                        label = $"{percent}%";
                    }

                    using (Font font = new Font("Segoe UI", 8.25f, FontStyle.Bold))
                    {
                        TextRenderer.DrawText(g, label, font, barRect, Color.FromArgb(50, 50, 50),
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

                        int fillWidth = (int)((barRect.Width - 2) * (Math.Clamp(percent, 0, 100) / 100.0));
                        if (fillWidth > 0)
                        {
                            Rectangle fillRect = new Rectangle(barRect.X + 1, barRect.Y + 1, fillWidth, barRect.Height - 2);

                            GraphicsState clipState = g.Save();
                            g.SetClip(fillRect);

                            using (SolidBrush fillBrush = new SolidBrush(fillColor))
                            {
                                g.FillRectangle(fillBrush, fillRect);
                            }

                            TextRenderer.DrawText(g, label, font, barRect, Color.White,
                                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

                            g.Restore(clipState);
                        }
                    }

                    g.DrawRectangle(borderPen, barRect);
                }
            }
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
                {
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
            }

            return bmp;
        }

        private void BtnAdd_Click(object? sender, EventArgs e)
        {
            AppLogger.Info("[Toolbar] Add URL button clicked.");
            using (AddUrlForm addUrlDlg = new AddUrlForm())
            {
                if (addUrlDlg.ShowDialog(this) == DialogResult.OK && addUrlDlg.ResultDownloadInfo != null)
                {
                    StartDownload(addUrlDlg.ResultDownloadInfo);
                }
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

            // Direct URLs without Cloudflare cookies use parallel segmented downloading;
            // Cloudflare-cleared URLs stream through WebView2's verified Chromium engine.
            if (string.IsNullOrWhiteSpace(info.Cookies))
            {
                LaunchSegmentedWorker(item, state);
            }
            else
            {
                LaunchWebView2Download(item, state);
            }
        }

        private void LaunchSegmentedWorker(ListViewItem item, DownloadItemState state)
        {
            state.Cts?.Cancel();
            state.Cts = new CancellationTokenSource();
            var token = state.Cts.Token;

            var worker = new SegmentedDownloadWorker(
                state.Info,
                onProgress: (percent, totalBytes, etaStr, speedStr) =>
                {
                    state.Percent = percent;
                    if (state.Info.TotalBytes <= 0 && totalBytes > 0)
                    {
                        state.Info.TotalBytes = totalBytes;
                    }

                    this.BeginInvoke(new Action(() =>
                    {
                        if (totalBytes > 0 && item.SubItems[1].Text == "Unknown")
                            item.SubItems[1].Text = FileInfoForm.FormatBytes(totalBytes);

                        item.SubItems[2].Text = totalBytes > 0 ? $"{percent}%" : "Downloading...";
                        item.SubItems[3].Text = etaStr;
                        item.SubItems[4].Text = speedStr;
                    }));
                },
                onCompleted: (success, statusLabel) =>
                {
                    if (success) state.Percent = 100;
                    this.BeginInvoke(new Action(() =>
                    {
                        item.SubItems[2].Text = statusLabel;
                        item.SubItems[3].Text = success ? "00:00" : "--:--";
                        item.SubItems[4].Text = success ? "--" : "0 KB/s";
                    }));
                },
                onFallbackToWebView2: () =>
                {
                    this.BeginInvoke(new Action(() => LaunchWebView2Download(item, state)));
                    return Task.CompletedTask;
                });

            _ = Task.Run(() => worker.RunAsync(token), token);
        }

        private void LaunchWebView2Download(ListViewItem item, DownloadItemState state)
        {
            var info = state.Info;

            if (!string.IsNullOrWhiteSpace(info.Cookies) && Uri.TryCreate(info.Url, UriKind.Absolute, out Uri? uri) && uri != null)
            {
                var cookieManager = _downloadEngine.CoreWebView2.CookieManager;
                var cookiePairs = info.Cookies.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var pair in cookiePairs)
                {
                    var parts = pair.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        var cookie = cookieManager.CreateCookie(parts[0].Trim(), parts[1].Trim(), uri.Host, "/");
                        cookieManager.AddOrUpdateCookie(cookie);
                    }
                }
            }

            lock (_pendingTasks)
            {
                _pendingTasks[info.Url] = (item, state);
            }

            AppLogger.Info($"[WebView2 Engine] Instructing engine to download: {info.Url}");
            _downloadEngine.CoreWebView2.Navigate(info.Url);
        }

        private void BtnResume_Click(object? sender, EventArgs e)
        {
            if (taskListView.SelectedItems.Count == 0) return;

            var selectedItem = taskListView.SelectedItems[0];
            if (selectedItem.Tag is not DownloadItemState state) return;

            string status = selectedItem.SubItems[2].Text;
            if (status == "Completed") return;

            if (state.DownloadOperation != null)
            {
                if (state.DownloadOperation.State == CoreWebView2DownloadState.Interrupted)
                {
                    AppLogger.Info($"[Download Action] Resuming WebView2 task: '{state.Info.FileName}'");
                    selectedItem.SubItems[2].Text = "Resuming...";
                    state.DownloadOperation.Resume();
                }
            }
            else if (status == "Stopped" || status == "Error")
            {
                AppLogger.Info($"[Download Action] Resuming segmented task: '{state.Info.FileName}'");
                selectedItem.SubItems[2].Text = "Resuming...";
                LaunchSegmentedWorker(selectedItem, state);
            }
        }

        private void BtnStop_Click(object? sender, EventArgs e)
        {
            if (taskListView.SelectedItems.Count == 0) return;

            var selectedItem = taskListView.SelectedItems[0];
            if (selectedItem.Tag is not DownloadItemState state) return;

            if (state.DownloadOperation != null && state.DownloadOperation.State == CoreWebView2DownloadState.InProgress)
            {
                AppLogger.Info($"[Download Action] Pausing WebView2 task: '{state.Info.FileName}'");
                state.DownloadOperation.Pause();
            }
            else if (state.Cts != null && !state.Cts.IsCancellationRequested)
            {
                AppLogger.Info($"[Download Action] Pausing segmented task: '{state.Info.FileName}'");
                state.Cts.Cancel();
            }
        }

        private void BtnStopAll_Click(object? sender, EventArgs e)
        {
            AppLogger.Info($"[Download Action] Stop All triggered across {_allDownloadItems.Count} total task(s).");
            foreach (var item in _allDownloadItems)
            {
                if (item.Tag is not DownloadItemState state) continue;

                if (state.DownloadOperation != null && state.DownloadOperation.State == CoreWebView2DownloadState.InProgress)
                    state.DownloadOperation.Pause();

                if (state.Cts != null && !state.Cts.IsCancellationRequested)
                    state.Cts.Cancel();
            }
        }

        private void BtnAbout_Click(object? sender, EventArgs e)
        {
            using (AboutForm aboutDlg = new AboutForm(this.Icon))
            {
                aboutDlg.ShowDialog(this);
            }
        }

        private void BtnLogs_Click(object? sender, EventArgs e)
        {
            var logsForm = new LogsForm(this.Icon);
            logsForm.Show(this);
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            HandleDeleteTask();
        }

        private void HandleDeleteTask()
        {
            if (taskListView.SelectedItems.Count == 0) return;

            var selectedItem = taskListView.SelectedItems[0];
            if (selectedItem.Tag is not DownloadItemState state) return;

            using (DeleteConfirmForm confirmDlg = new DeleteConfirmForm(state.Info.FileName, this.Icon))
            {
                if (confirmDlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (state.DownloadOperation != null && state.DownloadOperation.State == CoreWebView2DownloadState.InProgress)
                    {
                        state.DownloadOperation.Cancel();
                    }
                    state.Cts?.Cancel();

                    if (confirmDlg.UserChoice == DeleteActionChoice.DeletePermanently)
                    {
                        try
                        {
                            if (File.Exists(state.Info.FullSavePath))
                            {
                                File.Delete(state.Info.FullSavePath);
                                AppLogger.Info($"[Delete Permanently] Deleted file: '{state.Info.FullSavePath}'");
                            }
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Error($"[Delete Permanently] Error deleting file: {ex.Message}");
                        }
                    }

                    _allDownloadItems.Remove(selectedItem);
                    taskListView.Items.Remove(selectedItem);
                }
            }
        }

        private void BtnOptions_Click(object? sender, EventArgs e)
        {
            AppLogger.Info("[UI Action] Options dialog clicked.");
            using var optionsDlg = new OptionsForm(this.Icon);
            if (optionsDlg.ShowDialog(this) == DialogResult.OK)
            {
                // Refresh category tree in case categories were added/renamed/deleted
                InitializeCategoriesTree();
                ApplyCategoryFilter();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            BtnStopAll_Click(null, EventArgs.Empty);
            _downloadEngine?.Dispose();
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
                    using (Graphics g = Graphics.FromHwnd(_listView.Handle))
                    {
                        using (Pen pen = new Pen(Color.FromArgb(240, 240, 240)))
                        {
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
    }
}