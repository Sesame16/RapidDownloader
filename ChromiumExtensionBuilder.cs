using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Rapid_Downloader
{
    public class ChromiumExtensionBuilder : IBrowserExtensionBuilder
    {
        public string BrowserDisplayName => "Chromium Browsers";
        public string PackageFileName => "RapidDownloader.crx";
        public string InstallHint => "Drag onto chrome://extensions (Developer Mode)";

        private const string PrivateKeyFileName = "chromium_extension_key.pem";

        public string BuildPackage(AppSettings settings)
        {
            string extDir = AppSettings.GetExtensionsDirectory();
            string crxOutputPath = Path.Combine(extDir, PackageFileName);
            string keyPath = Path.Combine(extDir, PrivateKeyFileName);

            using RSA rsa = LoadOrCreateSigningKey(keyPath);
            byte[] zipBytes = CreateExtensionZipArchive(settings);
            byte[] crxBytes = PackSignedCrx3(zipBytes, rsa);

            File.WriteAllBytes(crxOutputPath, crxBytes);
            AppLogger.Info($"[Browser Integration] Built signed CRX3 package at '{crxOutputPath}' ({crxBytes.Length} bytes).");

            return crxOutputPath;
        }

        private static RSA LoadOrCreateSigningKey(string keyPath)
        {
            RSA rsa = RSA.Create(2048);
            try
            {
                if (File.Exists(keyPath))
                {
                    string pem = File.ReadAllText(keyPath, Encoding.UTF8);
                    rsa.ImportFromPem(pem);
                    return rsa;
                }

                string exportedPem = rsa.ExportRSAPrivateKeyPem();
                File.WriteAllText(keyPath, exportedPem, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"[Browser Integration] Regenerating CRX signing key: {ex.Message}");
                rsa.Dispose();
                rsa = RSA.Create(2048);
                File.WriteAllText(keyPath, rsa.ExportRSAPrivateKeyPem(), Encoding.UTF8);
            }

            return rsa;
        }

        private static byte[] CreateExtensionZipArchive(AppSettings settings)
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
                    service_worker = "background.js"
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

            return $@"// Rapid Downloader — Chromium Manifest V3 Service Worker
const BRIDGE_URL = ""http://127.0.0.1:{port}/api/download"";
const AUTH_TOKEN = ""{token}"";

let interceptionEnabled = true;

chrome.storage.local.get([""enabled""], (res) => {{
    interceptionEnabled = res.enabled !== false;
    updateBadge();
}});

function updateBadge() {{
    chrome.action.setBadgeText({{ text: interceptionEnabled ? ""ON"" : ""OFF"" }});
    chrome.action.setBadgeBackgroundColor({{ color: interceptionEnabled ? ""#107C41"" : ""#6E7781"" }});
}}

chrome.runtime.onInstalled.addListener(() => {{
    chrome.contextMenus.create({{
        id: ""rd-download-link"",
        title: ""Download with Rapid Downloader"",
        contexts: [""link""]
    }});
    updateBadge();
}});

chrome.action.onClicked.addListener(() => {{
    interceptionEnabled = !interceptionEnabled;
    chrome.storage.local.set({{ enabled: interceptionEnabled }});
    updateBadge();
}});

async function collectCookies(targetUrl) {{
    try {{
        const cookies = await chrome.cookies.getAll({{ url: targetUrl }});
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

chrome.contextMenus.onClicked.addListener(async (info, tab) => {{
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

chrome.downloads.onCreated.addListener(async (item) => {{
    if (!interceptionEnabled) return;

    const targetUrl = item.finalUrl || item.url;
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
            chrome.downloads.cancel(item.id, () => {{
                chrome.downloads.erase({{ id: item.id }});
            }});
        }}
    }} catch {{
        // Rapid Downloader is closed or unreachable; allow browser native download to continue
    }}
}});
";
        }

        /// <summary>
        /// Builds a valid CRX3 package containing an RSA-SHA256 signature over the SignedData header and ZIP payload.
        /// </summary>
        private static byte[] PackSignedCrx3(byte[] zipBytes, RSA rsa)
        {
            byte[] publicKeyDer = rsa.ExportSubjectPublicKeyInfo();

            // CRX ID is the first 16 bytes of SHA-256(SubjectPublicKeyInfo)
            byte[] crxId = SHA256.HashData(publicKeyDer).Take(16).ToArray();

            // Protobuf SignedData { bytes crx_id = 1; }
            byte[] signedHeaderData = EncodeProtobufBytesField(1, crxId);

            // Signature payload: "CRX3 SignedData\0" (16 bytes) + uint32_le(signedHeaderData.Length) + signedHeaderData + zipBytes
            byte[] contextPrefix = Encoding.ASCII.GetBytes("CRX3 SignedData\0");
            byte[] headerLenBytes = BitConverter.GetBytes((uint)signedHeaderData.Length);

            byte[] dataToSign = new byte[contextPrefix.Length + 4 + signedHeaderData.Length + zipBytes.Length];
            int offset = 0;
            Buffer.BlockCopy(contextPrefix, 0, dataToSign, offset, contextPrefix.Length);
            offset += contextPrefix.Length;
            Buffer.BlockCopy(headerLenBytes, 0, dataToSign, offset, 4);
            offset += 4;
            Buffer.BlockCopy(signedHeaderData, 0, dataToSign, offset, signedHeaderData.Length);
            offset += signedHeaderData.Length;
            Buffer.BlockCopy(zipBytes, 0, dataToSign, offset, zipBytes.Length);

            byte[] signature = rsa.SignData(dataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            // Protobuf AsymmetricKeyProof { bytes public_key = 1; bytes signature = 2; }
            byte[] keyProof = CombineBytes(
                EncodeProtobufBytesField(1, publicKeyDer),
                EncodeProtobufBytesField(2, signature)
            );

            // Protobuf CrxFileHeader { repeated AsymmetricKeyProof sha256_with_rsa = 2; optional bytes signed_header_data = 10000; }
            byte[] crxHeader = CombineBytes(
                EncodeProtobufBytesField(2, keyProof),
                EncodeProtobufBytesField(10000, signedHeaderData)
            );

            // CRX3 binary container: "Cr24" + uint32_le(3) + uint32_le(crxHeader.Length) + crxHeader + zipBytes
            using var output = new MemoryStream();
            output.Write(Encoding.ASCII.GetBytes("Cr24"));
            output.Write(BitConverter.GetBytes((uint)3));
            output.Write(BitConverter.GetBytes((uint)crxHeader.Length));
            output.Write(crxHeader);
            output.Write(zipBytes);

            return output.ToArray();
        }

        private static byte[] EncodeProtobufBytesField(int fieldNumber, byte[] data)
        {
            uint tag = (uint)((fieldNumber << 3) | 2); // Wire type 2: length-delimited
            byte[] tagBytes = EncodeVarint(tag);
            byte[] lenBytes = EncodeVarint((uint)data.Length);
            return CombineBytes(tagBytes, lenBytes, data);
        }

        private static byte[] EncodeVarint(uint value)
        {
            using var ms = new MemoryStream();
            while (value >= 0x80)
            {
                ms.WriteByte((byte)((value & 0x7F) | 0x80));
                value >>= 7;
            }
            ms.WriteByte((byte)value);
            return ms.ToArray();
        }

        private static byte[] CombineBytes(params byte[][] arrays)
        {
            int total = arrays.Sum(a => a.Length);
            byte[] result = new byte[total];
            int pos = 0;
            foreach (var arr in arrays)
            {
                Buffer.BlockCopy(arr, 0, result, pos, arr.Length);
                pos += arr.Length;
            }
            return result;
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

                using (var bgBrush = new SolidBrush(Color.FromArgb(0, 120, 215)))
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