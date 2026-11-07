using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace Rapid_Downloader
{
    public static class WebView2Helper
    {
        private static CoreWebView2Environment? _environment;
        private static readonly SemaphoreSlim _initLock = new(1, 1);

        public static async Task<CoreWebView2Environment> GetEnvironmentAsync()
        {
            if (_environment != null) return _environment;

            await _initLock.WaitAsync();
            try
            {
                if (_environment != null) return _environment;

                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RapidDownloader_SharedProfile"
                );

                // Configure Chromium preference to ALWAYS download PDFs instead of opening in viewer
                ConfigureAlwaysDownloadPdf(userDataFolder);

                var options = new CoreWebView2EnvironmentOptions
                {
                    AdditionalBrowserArguments = "--disable-features=PdfViewer,EdgeNewPDFViewer"
                };

                _environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
                return _environment;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private static void ConfigureAlwaysDownloadPdf(string userDataFolder)
        {
            try
            {
                string defaultDir = Path.Combine(userDataFolder, "Default");
                Directory.CreateDirectory(defaultDir);
                string prefsPath = Path.Combine(defaultDir, "Preferences");

                JsonNode rootNode = new JsonObject();
                if (File.Exists(prefsPath))
                {
                    try
                    {
                        string existingJson = File.ReadAllText(prefsPath);
                        rootNode = JsonNode.Parse(existingJson) ?? new JsonObject();
                    }
                    catch { }
                }

                if (rootNode["plugins"] == null)
                {
                    rootNode["plugins"] = new JsonObject();
                }

                rootNode["plugins"]!["always_open_pdf_externally"] = true;
                File.WriteAllText(prefsPath, rootNode.ToJsonString());
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"[WebView2] Could not write PDF preferences: {ex.Message}");
            }
        }
    }
}