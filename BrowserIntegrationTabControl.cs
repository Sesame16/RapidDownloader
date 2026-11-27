using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Rapid_Downloader
{
    public class BrowserIntegrationTabControl : UserControl, IOptionsTab
    {
        private CheckBox chkEnableBridge = null!;
        private Label lblPort = null!;
        private NumericUpDown numBridgePort = null!;

        private BrowserDragCard cardChromium = null!;
        private BrowserDragCard cardFirefox = null!;

        private Label lblStatus = null!;
        private Button btnOpenFolder = null!;
        private Button btnRebuildPackages = null!;

        private readonly IBrowserExtensionBuilder _chromiumBuilder;
        private readonly IBrowserExtensionBuilder _firefoxBuilder;

        private AppSettings _workingSettings = new();

        public BrowserIntegrationTabControl()
        {
            _chromiumBuilder = new ChromiumExtensionBuilder();
            _firefoxBuilder = new FirefoxExtensionBuilder();

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.White;
            this.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            // 1. Top Section Banner
            var bannerPanel = new Panel
            {
                Location = new Point(16, 12),
                Size = new Size(640, 36)
            };

            var picIcon = new PictureBox
            {
                Image = SystemIcons.Application.ToBitmap(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(4, 4),
                Size = new Size(28, 28)
            };

            var lblHeader = new Label
            {
                Text = "Browser Integration (Drag & Drop Extension Installer)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Location = new Point(40, 7),
                AutoSize = true,
                ForeColor = Color.FromArgb(40, 40, 40)
            };

            bannerPanel.Controls.AddRange(new Control[] { picIcon, lblHeader });

            // 2. Bridge Service Configuration Bar
            var grpBridge = new GroupBox
            {
                Text = "Local Extension Bridge",
                Location = new Point(16, 52),
                Size = new Size(640, 64),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            chkEnableBridge = new CheckBox
            {
                Text = "Enable browser download interception listener",
                Location = new Point(16, 26),
                AutoSize = true,
                Checked = true
            };
            chkEnableBridge.CheckedChanged += (s, e) => UpdateBridgeControlsState();

            var flowPort = new FlowLayoutPanel
            {
                Location = new Point(455, 22),
                Size = new Size(170, 30),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            lblPort = new Label
            {
                Text = "Loopback Port:",
                AutoSize = true,
                Margin = new Padding(0, 5, 6, 0)
            };

            numBridgePort = new NumericUpDown
            {
                Size = new Size(72, 25),
                Minimum = 1024,
                Maximum = 65535,
                Value = 45890,
                Margin = new Padding(0, 1, 0, 0)
            };

            flowPort.Controls.AddRange(new Control[] { lblPort, numBridgePort });
            grpBridge.Controls.AddRange(new Control[] { chkEnableBridge, flowPort });

            // 3. Side-by-Side Drag-and-Drop Browser Cards
            cardChromium = new BrowserDragCard(
                _chromiumBuilder,
                borderColor: Color.FromArgb(30, 30, 30),
                accentColor: Color.FromArgb(0, 120, 215),
                badgeText: "CRX",
                onDragRequested: StartExtensionDrag
            )
            {
                Location = new Point(42, 130),
                Size = new Size(272, 268)
            };

            cardFirefox = new BrowserDragCard(
                _firefoxBuilder,
                borderColor: Color.FromArgb(245, 184, 0),
                accentColor: Color.FromArgb(230, 96, 0),
                badgeText: "XPI",
                onDragRequested: StartExtensionDrag
            )
            {
                Location = new Point(358, 130),
                Size = new Size(272, 268)
            };

            // 4. Bottom Status & Utility Actions
            lblStatus = new Label
            {
                Text = "Click and drag either card directly into your browser's Extensions / Add-ons window to install.",
                Location = new Point(16, 412),
                Size = new Size(640, 20),
                ForeColor = Color.FromArgb(70, 70, 70),
                TextAlign = ContentAlignment.MiddleCenter
            };

            btnOpenFolder = new Button
            {
                Text = "Open Extension Folder",
                Location = new Point(16, 442),
                Size = new Size(165, 28)
            };
            btnOpenFolder.Click += BtnOpenFolder_Click;

            btnRebuildPackages = new Button
            {
                Text = "Rebuild .crx / .xpi",
                Location = new Point(190, 442),
                Size = new Size(145, 28)
            };
            btnRebuildPackages.Click += BtnRebuildPackages_Click;

            var lblTip = new Label
            {
                Text = "Chromium: enable Developer Mode first.\nFirefox: drag to about:addons or load via about:debugging.",
                Location = new Point(348, 439),
                Size = new Size(308, 34),
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8f, FontStyle.Regular)
            };

            this.Controls.AddRange(new Control[]
            {
                bannerPanel,
                grpBridge,
                cardChromium,
                cardFirefox,
                lblStatus,
                btnOpenFolder,
                btnRebuildPackages,
                lblTip
            });
        }

        public void LoadSettings(AppSettings settings)
        {
            _workingSettings = new AppSettings
            {
                EnableBrowserIntegration = settings.EnableBrowserIntegration,
                BridgePort = settings.BridgePort > 0 ? settings.BridgePort : 45890,
                BridgeAuthToken = settings.BridgeAuthToken
            };

            chkEnableBridge.Checked = _workingSettings.EnableBrowserIntegration;
            numBridgePort.Value = Math.Clamp(_workingSettings.BridgePort, 1024, 65535);
            UpdateBridgeControlsState();

            // Pre-generate packages quietly so they are immediately ready on disk
            EnsurePackagesBuilt(silent: true);
        }

        public bool ValidateSettings(out string errorMessage)
        {
            if (numBridgePort.Value < 1024 || numBridgePort.Value > 65535)
            {
                errorMessage = "Loopback port must be between 1024 and 65535.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public void ApplySettings(AppSettings settings)
        {
            bool portChanged = settings.BridgePort != (int)numBridgePort.Value;

            settings.EnableBrowserIntegration = chkEnableBridge.Checked;
            settings.BridgePort = (int)numBridgePort.Value;

            _workingSettings.EnableBrowserIntegration = settings.EnableBrowserIntegration;
            _workingSettings.BridgePort = settings.BridgePort;
            _workingSettings.BridgeAuthToken = settings.BridgeAuthToken;

            if (portChanged)
            {
                EnsurePackagesBuilt(silent: true);
            }
        }

        private void UpdateBridgeControlsState()
        {
            bool enabled = chkEnableBridge.Checked;
            lblPort.Enabled = enabled;
            numBridgePort.Enabled = enabled;
        }

        private void StartExtensionDrag(IBrowserExtensionBuilder builder)
        {
            try
            {
                _workingSettings.BridgePort = (int)numBridgePort.Value;
                string packagePath = builder.BuildPackage(_workingSettings);

                if (!File.Exists(packagePath))
                {
                    lblStatus.ForeColor = Color.Crimson;
                    lblStatus.Text = $"Failed to locate generated package: {builder.PackageFileName}";
                    return;
                }

                lblStatus.ForeColor = Color.FromArgb(0, 102, 204);
                lblStatus.Text = $"Dragging '{builder.PackageFileName}' — drop it into your {builder.BrowserDisplayName} window...";

                var dataObj = new DataObject(DataFormats.FileDrop, new[] { packagePath });
                DragDropEffects result = DoDragDrop(dataObj, DragDropEffects.Copy);

                if (result != DragDropEffects.None)
                {
                    lblStatus.ForeColor = Color.FromArgb(16, 124, 65);
                    lblStatus.Text = $"Dropped '{builder.PackageFileName}' into {builder.BrowserDisplayName}. Confirm installation in your browser.";
                }
                else
                {
                    lblStatus.ForeColor = Color.FromArgb(70, 70, 70);
                    lblStatus.Text = $"Ready: {builder.InstallHint}";
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[Browser Integration] Drag operation failed for {builder.BrowserDisplayName}", ex);
                lblStatus.ForeColor = Color.Crimson;
                lblStatus.Text = $"Error building {builder.PackageFileName}: {ex.Message}";
            }
        }

        private void EnsurePackagesBuilt(bool silent)
        {
            try
            {
                _workingSettings.BridgePort = (int)numBridgePort.Value;
                _chromiumBuilder.BuildPackage(_workingSettings);
                _firefoxBuilder.BuildPackage(_workingSettings);

                if (!silent)
                {
                    lblStatus.ForeColor = Color.FromArgb(16, 124, 65);
                    lblStatus.Text = $"Rebuilt {_chromiumBuilder.PackageFileName} and {_firefoxBuilder.PackageFileName} for port {_workingSettings.BridgePort}.";
                }
            }
            catch (Exception ex)
            {
                if (!silent)
                {
                    lblStatus.ForeColor = Color.Crimson;
                    lblStatus.Text = $"Rebuild failed: {ex.Message}";
                }
            }
        }

        private void BtnOpenFolder_Click(object? sender, EventArgs e)
        {
            EnsurePackagesBuilt(silent: true);
            string dir = AppSettings.GetExtensionsDirectory();
            Process.Start("explorer.exe", $"\"{dir}\"");
        }

        private void BtnRebuildPackages_Click(object? sender, EventArgs e)
        {
            EnsurePackagesBuilt(silent: false);
        }

        /// <summary>
        /// Custom-drawn interactive card matching the wireframe: thick colored frame,
        /// rotated diamond icon badge in the upper-middle, and bold browser title below.
        /// </summary>
        private sealed class BrowserDragCard : Panel
        {
            private readonly IBrowserExtensionBuilder _builder;
            private readonly Color _borderColor;
            private readonly Color _accentColor;
            private readonly string _badgeText;
            private readonly Action<IBrowserExtensionBuilder> _onDragRequested;

            private bool _isHovered;
            private bool _isMouseDown;
            private Point _mouseDownPoint;

            public BrowserDragCard(
                IBrowserExtensionBuilder builder,
                Color borderColor,
                Color accentColor,
                string badgeText,
                Action<IBrowserExtensionBuilder> onDragRequested)
            {
                _builder = builder;
                _borderColor = borderColor;
                _accentColor = accentColor;
                _badgeText = badgeText;
                _onDragRequested = onDragRequested;

                this.DoubleBuffered = true;
                this.Cursor = Cursors.Hand;
                this.BackColor = Color.White;
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                _isHovered = true;
                this.Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _isHovered = false;
                this.Invalidate();
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button == MouseButtons.Left)
                {
                    _isMouseDown = true;
                    _mouseDownPoint = e.Location;
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (_isMouseDown && e.Button == MouseButtons.Left)
                {
                    int dx = Math.Abs(e.X - _mouseDownPoint.X);
                    int dy = Math.Abs(e.Y - _mouseDownPoint.Y);
                    if (dx >= 5 || dy >= 5)
                    {
                        _isMouseDown = false;
                        _onDragRequested(_builder);
                    }
                }
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                _isMouseDown = false;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // Subtle hover fill
                Color bg = _isHovered ? Color.FromArgb(247, 250, 255) : Color.White;
                using (var bgBrush = new SolidBrush(bg))
                {
                    g.FillRectangle(bgBrush, this.ClientRectangle);
                }

                // Thick 3px outer border (black for Chromium, gold for FireFox)
                int borderThickness = 3;
                Rectangle borderRect = new Rectangle(
                    borderThickness / 2,
                    borderThickness / 2,
                    this.Width - borderThickness,
                    this.Height - borderThickness);

                using (var borderPen = new Pen(_borderColor, borderThickness))
                {
                    g.DrawRectangle(borderPen, borderRect);
                }

                // Rotated Diamond Icon Frame in upper-center
                int cx = this.Width / 2;
                int cy = 92;
                int halfDiag = 48;

                Point[] diamondPoints =
                {
                    new Point(cx, cy - halfDiag), // Top
                    new Point(cx + halfDiag, cy), // Right
                    new Point(cx, cy + halfDiag), // Bottom
                    new Point(cx - halfDiag, cy)  // Left
                };

                using (var diamondFill = new SolidBrush(_isHovered ? Color.FromArgb(236, 244, 255) : Color.FromArgb(250, 250, 252)))
                {
                    g.FillPolygon(diamondFill, diamondPoints);
                }

                using (var diamondPen = new Pen(Color.FromArgb(35, 35, 35), 2.2f))
                {
                    g.DrawPolygon(diamondPen, diamondPoints);
                }

                // Extension format badge inside the diamond ("CRX" / "XPI") + download arrow
                using (var badgeFont = new Font("Segoe UI", 11f, FontStyle.Bold))
                using (var badgeBrush = new SolidBrush(_accentColor))
                {
                    var badgeRect = new Rectangle(cx - 36, cy - 18, 72, 22);
                    TextRenderer.DrawText(g, _badgeText, badgeFont, badgeRect, _accentColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }

                using (var arrowPen = new Pen(_accentColor, 2f))
                {
                    arrowPen.StartCap = LineCap.Round;
                    arrowPen.EndCap = LineCap.Round;
                    int ayTop = cy + 7;
                    int ayBottom = cy + 22;
                    g.DrawLine(arrowPen, cx, ayTop, cx, ayBottom);
                    g.DrawLine(arrowPen, cx - 6, ayBottom - 6, cx, ayBottom);
                    g.DrawLine(arrowPen, cx + 6, ayBottom - 6, cx, ayBottom);
                }

                // Browser Display Title below the diamond
                using (var titleFont = new Font("Segoe UI", 12.5f, FontStyle.Bold))
                {
                    var titleRect = new Rectangle(16, 162, this.Width - 32, 34);
                    TextRenderer.DrawText(g, _builder.BrowserDisplayName, titleFont, titleRect, Color.FromArgb(25, 25, 25),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }

                // Drag action pill/subtitle
                using (var subFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
                {
                    var pillRect = new Rectangle(24, 204, this.Width - 48, 38);
                    TextRenderer.DrawText(g, $"Drag {_builder.PackageFileName}\n({_builder.InstallHint})", subFont, pillRect,
                        Color.FromArgb(95, 95, 95),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
                }
            }
        }
    }
}