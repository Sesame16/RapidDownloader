using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Rapid_Downloader
{
    public class FirefoxExtensionBuilder : IBrowserExtensionBuilder
    {
        public string BrowserDisplayName => "FireFox";
        public string PackageFileName => "RapidDownloader.xpi";
        public string InstallHint => "Drag onto about:addons (or load via about:debugging)";

        public string BuildPackage(AppSettings settings)
        {
            string extDir = AppSettings.GetExtensionsDirectory();
            string xpiOutputPath = Path.Combine(extDir, PackageFileName);

            byte[] xpiBytes = CreateExtensionXpiArchive(settings);
            File.WriteAllBytes(xpiOutputPath, xpiBytes);

            AppLogger.Info($"[Browser Integration] Built Firefox XPI package at '{xpiOutputPath}' ({xpiBytes.Length} bytes).");
            return xpiOutputPath;
        }

        private static byte[] CreateExtensionXpiArchive(AppSettings settings)
        {
            using var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                AddTextEntry(archive, "manifest.json", GenerateManifestJson());
                AddTextEntry(archive, "background.js", GenerateBackgroundJs(settings));
                AddBinaryEntry(archive, "icon16.png", RenderPngIcon(16));
                AddBinaryEntry(archive, "icon48.png", RenderPngIcon(48));
                AddBinaryEntry(archive, "icon128.png", RenderPngIcon(128));
            }

            return ms.ToArray();
        }

        private static string GenerateManifestJson()
        {
            var manifest = new
            {
                manifest_version = 3,
                name = "Rapid Downloader Integration",
                version = "1.0.0",
                description = "Captures browser downloads and forwards URLs, referers, and session cookies to Rapid Downloader.",
                browser_specific_settings = new
                {
                    gecko = new
                    {
                        id = "rapid-downloader@local.addon",
                        strict_min_version = "109.0"
                    }
                },
                permissions = new[]
                {
                    "downloads",
                    "cookies",
                    "storage",
                    "contextMenus"
                },
                host_permissions = new[]
                {
                    "<all_urls>",
                    "http://127.0.0.1/*"
                },
                background = new
                {
                    scripts = new[] { "background.js" }
                },
                icons = new
                {
                    _16 = "icon16.png",
                    _48 = "icon48.png",
                    _128 = "icon128.png"
                },
                action = new
                {
                    default_title = "Rapid Downloader Integration (Click to Toggle)",
                    default_icon = new
                    {
                        _16 = "icon16.png",
                        _48 = "icon48.png"
                    }
                }
            };

            string rawJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            return rawJson
                .Replace("\"_16\":", "\"16\":")
                .Replace("\"_48\":", "\"48\":")
                .Replace("\"_128\":", "\"128\":");
        }

        private static string GenerateBackgroundJs(AppSettings settings)
        {
            int port = settings.BridgePort > 0 ? settings.BridgePort : 45890;
            string token = settings.BridgeAuthToken ?? string.Empty;

            return $@"// Rapid Downloader — Firefox WebExtension Background Script
const api = typeof browser !== ""undefined"" ? browser : chrome;
const BRIDGE_URL = ""http://127.0.0.1:{port}/api/download"";
const AUTH_TOKEN = ""{token}"";

let interceptionEnabled = true;

api.storage.local.get([""enabled""]).then((res) => {{
    interceptionEnabled = res.enabled !== false;
    updateBadge();
}}).catch(() => updateBadge());

function updateBadge() {{
    api.action.setBadgeText({{ text: interceptionEnabled ? ""ON"" : ""OFF"" }});
    api.action.setBadgeBackgroundColor({{ color: interceptionEnabled ? ""#107C41"" : ""#6E7781"" }});
}}

api.runtime.onInstalled.addListener(() => {{
    api.contextMenus.create({{
        id: ""rd-download-link"",
        title: ""Download with Rapid Downloader"",
        contexts: [""link""]
    }});
    updateBadge();
}});

api.action.onClicked.addListener(() => {{
    interceptionEnabled = !interceptionEnabled;
    api.storage.local.set({{ enabled: interceptionEnabled }});
    updateBadge();
}});

async function collectCookies(targetUrl) {{
    try {{
        const cookies = await api.cookies.getAll({{ url: targetUrl }});
        if (!cookies || cookies.length === 0) return """";
        return cookies.map(c => `${{c.name}}=${{c.value}}`).join(""; "");
    }} catch {{
        return """";
    }}
}}

async function dispatchToRapidDownloader(payload) {{
    const response = await fetch(BRIDGE_URL, {{
        method: ""POST"",
        headers: {{
            ""Content-Type"": ""application/json"",
            ""X-RD-Token"": AUTH_TOKEN
        }},
        body: JSON.stringify(payload)
    }});
    return response.ok;
}}

api.contextMenus.onClicked.addListener(async (info, tab) => {{
    if (info.menuItemId !== ""rd-download-link"" || !info.linkUrl) return;

    const cookies = await collectCookies(info.linkUrl);
    try {{
        await dispatchToRapidDownloader({{
            url: info.linkUrl,
            filename: """",
            referer: info.pageUrl || (tab ? tab.url : """") || """",
            cookies: cookies,
            userAgent: navigator.userAgent
        }});
    }} catch (err) {{
        console.warn(""[Rapid Downloader] Bridge unreachable:"", err);
    }}
}});

api.downloads.onCreated.addListener(async (item) => {{
    if (!interceptionEnabled) return;

    const targetUrl = item.url;
    if (!targetUrl || !/^https?:\/\//i.test(targetUrl)) return;

    const cookies = await collectCookies(targetUrl);

    try {{
        const accepted = await dispatchToRapidDownloader({{
            url: targetUrl,
            filename: item.filename || """",
            referer: item.referrer || """",
            cookies: cookies,
            userAgent: navigator.userAgent
        }});

        if (accepted) {{
            await api.downloads.cancel(item.id);
            await api.downloads.erase({{ id: item.id }});
        }}
    }} catch {{
        // Rapid Downloader is closed or unreachable; allow Firefox native download to continue
    }}
}});
";
        }

        private static void AddTextEntry(ZipArchive archive, string entryName, string content)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(content);
        }

        private static void AddBinaryEntry(ZipArchive archive, string entryName, byte[] bytes)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var stream = entry.Open();
            stream.Write(bytes, 0, bytes.Length);
        }

        private static byte[] RenderPngIcon(int size)
        {
            using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using (var bgBrush = new SolidBrush(Color.FromArgb(230, 96, 0)))
                {
                    g.FillEllipse(bgBrush, 1, 1, size - 2, size - 2);
                }

                using (var pen = new Pen(Color.White, Math.Max(1.5f, size / 8f)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    float mid = size / 2f;
                    float top = size * 0.26f;
                    float bottom = size * 0.72f;
                    float wing = size * 0.22f;

                    g.DrawLine(pen, mid, top, mid, bottom);
                    g.DrawLine(pen, mid - wing, bottom - wing, mid, bottom);
                    g.DrawLine(pen, mid + wing, bottom - wing, mid, bottom);
                }
            }

            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }
}