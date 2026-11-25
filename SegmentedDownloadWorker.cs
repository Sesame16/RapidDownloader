using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rapid_Downloader
{
    public class SegmentedDownloadWorker
    {
        private readonly DownloadInfo _info;
        private readonly Action<int, long, string, string> _onProgress;
        private readonly Action<bool, string> _onCompleted;
        private readonly Func<Task>? _onFallbackToWebView2;

        public SegmentedDownloadWorker(
            DownloadInfo info,
            Action<int, long, string, string> onProgress,
            Action<bool, string> onCompleted,
            Func<Task>? onFallbackToWebView2 = null)
        {
            _info = info;
            _onProgress = onProgress;
            _onCompleted = onCompleted;
            _onFallbackToWebView2 = onFallbackToWebView2;
        }

        public async Task RunAsync(CancellationToken token)
        {
            try
            {
                Directory.CreateDirectory(_info.SaveDirectory);
                string tempRoot = AppSettings.Current.TempDirectory;
                Directory.CreateDirectory(tempRoot);

                var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    MaxAutomaticRedirections = 5,
                    AutomaticDecompression = DecompressionMethods.None,
                    UseCookies = false,
                    MaxConnectionsPerServer = 64
                };

                using var client = new HttpClient(handler)
                {
                    Timeout = Timeout.InfiniteTimeSpan
                };

                if (!string.IsNullOrWhiteSpace(_info.UserAgent))
                {
                    client.DefaultRequestHeaders.UserAgent.Clear();
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(_info.UserAgent);
                }
                else
                {
                    AddUrlForm.ApplyBrowserHeaders(client);
                }

                // Probe range support & server metadata
                using var probeReq = new HttpRequestMessage(HttpMethod.Get, _info.Url);
                if (Uri.TryCreate(_info.Url, UriKind.Absolute, out Uri? parsedUri) && parsedUri != null)
                    probeReq.Headers.Referrer = AddUrlForm.GetRefererUri(parsedUri);
                if (!string.IsNullOrWhiteSpace(_info.Cookies))
                    probeReq.Headers.TryAddWithoutValidation("Cookie", _info.Cookies);
                probeReq.Headers.Range = new RangeHeaderValue(0, 0);

                using var probeResp = await client.SendAsync(probeReq, HttpCompletionOption.ResponseHeadersRead, token);

                if ((int)probeResp.StatusCode == 403 && _onFallbackToWebView2 != null)
                {
                    AppLogger.Warn($"[Segmented Worker] Server returned 403 for '{_info.FileName}'. Handing off to WebView2 engine...");
                    await _onFallbackToWebView2();
                    return;
                }

                probeResp.EnsureSuccessStatusCode();

                bool supportsRanges = probeResp.StatusCode == HttpStatusCode.PartialContent ||
                                      probeResp.Headers.AcceptRanges.Contains("bytes");

                long totalBytes = _info.TotalBytes;
                if (probeResp.Content.Headers.ContentRange?.Length.HasValue == true)
                    totalBytes = probeResp.Content.Headers.ContentRange.Length.Value;
                else if (totalBytes <= 0 && probeResp.Content.Headers.ContentLength.HasValue)
                    totalBytes = probeResp.Content.Headers.ContentLength.Value;

                DateTimeOffset? serverLastModified = probeResp.Content.Headers.LastModified;

                // Determine optimal connection count (at least 1 MB per segment)
                int configuredConnections = AppSettings.Current.GetConnectionsForUrl(_info.Url);
                int segmentCount = 1;

                if (supportsRanges && totalBytes >= 2 * 1024 * 1024)
                {
                    int maxUsefulSegments = (int)(totalBytes / (1024 * 1024));
                    segmentCount = Math.Clamp(configuredConnections, 1, Math.Max(1, maxUsefulSegments));
                }

                AppLogger.Info($"[Segmented Worker] Starting '{_info.FileName}' | Size: {FileInfoForm.FormatBytes(totalBytes)} | Segments: {segmentCount} | TempDir: '{tempRoot}'");

                string taskHash = ComputeShortHash(_info.Url + "|" + _info.FullSavePath);
                string taskTempDir = Path.Combine(tempRoot, $"rd_{taskHash}_{segmentCount}");
                Directory.CreateDirectory(taskTempDir);

                if (segmentCount == 1)
                {
                    await DownloadSingleStreamAsync(client, taskTempDir, totalBytes, supportsRanges, serverLastModified, token);
                }
                else
                {
                    await DownloadMultiSegmentAsync(client, taskTempDir, totalBytes, segmentCount, serverLastModified, token);
                }
            }
            catch (OperationCanceledException)
            {
                AppLogger.Info($"[Segmented Worker] Paused '{_info.FileName}'.");
                _onCompleted(false, "Stopped");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[Segmented Worker] Failed '{_info.FileName}'", ex);
                _onCompleted(false, "Error");
            }
        }

        private async Task DownloadSingleStreamAsync(
            HttpClient client,
            string taskTempDir,
            long totalBytes,
            bool supportsRanges,
            DateTimeOffset? serverLastModified,
            CancellationToken token)
        {
            string partPath = Path.Combine(taskTempDir, "part_0.tmp");
            long existing = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;
            if (!supportsRanges) existing = 0;

            using var req = new HttpRequestMessage(HttpMethod.Get, _info.Url);
            if (!string.IsNullOrWhiteSpace(_info.Cookies))
                req.Headers.TryAddWithoutValidation("Cookie", _info.Cookies);
            if (existing > 0)
                req.Headers.Range = new RangeHeaderValue(existing, null);

            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token);
            resp.EnsureSuccessStatusCode();

            bool append = resp.StatusCode == HttpStatusCode.PartialContent;
            if (!append) existing = 0;

            if (totalBytes <= 0 && resp.Content.Headers.ContentLength.HasValue)
                totalBytes = existing + resp.Content.Headers.ContentLength.Value;

            using var stream = await resp.Content.ReadAsStreamAsync(token);
            using (var fs = new FileStream(partPath, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                byte[] buffer = new byte[81920];
                long totalRead = existing;
                var sw = Stopwatch.StartNew();
                long lastBytes = totalRead;
                long lastTime = 0;

                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    await fs.WriteAsync(buffer, 0, read, token);
                    totalRead += read;

                    long now = sw.ElapsedMilliseconds;
                    if (now - lastTime >= 400)
                    {
                        double sec = (now - lastTime) / 1000.0;
                        double speed = (totalRead - lastBytes) / sec;
                        lastBytes = totalRead;
                        lastTime = now;

                        ReportProgress(totalRead, totalBytes, speed);
                    }
                }
            }

            FinalizeFile(new[] { partPath }, taskTempDir, serverLastModified);
            _onCompleted(true, "Completed");
        }

        private async Task DownloadMultiSegmentAsync(
            HttpClient client,
            string taskTempDir,
            long totalBytes,
            int segmentCount,
            DateTimeOffset? serverLastModified,
            CancellationToken token)
        {
            long chunkSize = totalBytes / segmentCount;
            var partPaths = new string[segmentCount];
            long totalDownloaded = 0;

            for (int i = 0; i < segmentCount; i++)
            {
                partPaths[i] = Path.Combine(taskTempDir, $"part_{i}.tmp");
                if (File.Exists(partPaths[i]))
                {
                    long len = new FileInfo(partPaths[i]).Length;
                    long expectedMax = (i == segmentCount - 1) ? (totalBytes - i * chunkSize) : chunkSize;
                    if (len > expectedMax)
                    {
                        File.Delete(partPaths[i]);
                    }
                    else
                    {
                        totalDownloaded += len;
                    }
                }
            }

            using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var progressTask = Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                long lastBytes = Interlocked.Read(ref totalDownloaded);
                long lastTime = 0;

                while (!progressCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(450, progressCts.Token).ConfigureAwait(false);
                    long now = sw.ElapsedMilliseconds;
                    long current = Interlocked.Read(ref totalDownloaded);
                    double sec = Math.Max(0.1, (now - lastTime) / 1000.0);
                    double speed = Math.Max(0, (current - lastBytes) / sec);

                    lastBytes = current;
                    lastTime = now;

                    ReportProgress(current, totalBytes, speed);
                }
            }, progressCts.Token);

            var segmentTasks = new List<Task>();
            for (int i = 0; i < segmentCount; i++)
            {
                int index = i;
                long rangeStart = index * chunkSize;
                long rangeEnd = (index == segmentCount - 1) ? (totalBytes - 1) : ((index + 1) * chunkSize - 1);
                string partFile = partPaths[index];

                segmentTasks.Add(Task.Run(async () =>
                {
                    long existing = File.Exists(partFile) ? new FileInfo(partFile).Length : 0;
                    long currentStart = rangeStart + existing;
                    if (currentStart > rangeEnd) return; // Segment already finished

                    using var req = new HttpRequestMessage(HttpMethod.Get, _info.Url);
                    if (!string.IsNullOrWhiteSpace(_info.Cookies))
                        req.Headers.TryAddWithoutValidation("Cookie", _info.Cookies);
                    req.Headers.Range = new RangeHeaderValue(currentStart, rangeEnd);

                    using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token);
                    resp.EnsureSuccessStatusCode();

                    using var stream = await resp.Content.ReadAsStreamAsync(token);
                    using var fs = new FileStream(partFile, existing > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                    byte[] buffer = new byte[81920];
                    int read;
                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                    {
                        await fs.WriteAsync(buffer, 0, read, token);
                        Interlocked.Add(ref totalDownloaded, read);
                    }
                }, token));
            }

            try
            {
                await Task.WhenAll(segmentTasks);
            }
            finally
            {
                progressCts.Cancel();
                try { await progressTask; } catch { }
            }

            FinalizeFile(partPaths, taskTempDir, serverLastModified);
            _onCompleted(true, "Completed");
        }

        private void FinalizeFile(string[] partPaths, string taskTempDir, DateTimeOffset? serverLastModified)
        {
            string finalPath = _info.FullSavePath;
            using (var outFs = new FileStream(finalPath, FileMode.Create, FileAccess.Write, FileShare.None, 131072))
            {
                foreach (string part in partPaths)
                {
                    if (!File.Exists(part)) continue;
                    using var inFs = new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.Read, 131072);
                    inFs.CopyTo(outFs);
                }
            }

            if (AppSettings.Current.SetFileCreationDateFromServer && serverLastModified.HasValue)
            {
                try
                {
                    DateTime utcTime = serverLastModified.Value.UtcDateTime;
                    File.SetCreationTimeUtc(finalPath, utcTime);
                    File.SetLastWriteTimeUtc(finalPath, utcTime);
                }
                catch (Exception ex)
                {
                    AppLogger.Warn($"[Segmented Worker] Could not apply server timestamp: {ex.Message}");
                }
            }

            try
            {
                if (Directory.Exists(taskTempDir))
                    Directory.Delete(taskTempDir, true);
            }
            catch { }

            AppLogger.Info($"[Segmented Worker] Assembled '{_info.FileName}' -> '{finalPath}'");
        }

        private void ReportProgress(long downloaded, long totalBytes, double speedBytesPerSec)
        {
            int percent = totalBytes > 0 ? (int)Math.Clamp((downloaded * 100) / totalBytes, 0, 100) : 0;

            string etaStr = "--:--";
            if (totalBytes > 0 && speedBytesPerSec > 0)
            {
                long remaining = Math.Max(0, totalBytes - downloaded);
                var eta = TimeSpan.FromSeconds(remaining / speedBytesPerSec);
                etaStr = eta.TotalHours >= 1 ? eta.ToString(@"hh\:mm\:ss") : eta.ToString(@"mm\:ss");
            }

            string speedStr = $"{FileInfoForm.FormatBytes((long)speedBytesPerSec)}/s";
            _onProgress(percent, totalBytes, etaStr, speedStr);
        }

        private static string ComputeShortHash(string input)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
        }
    }
}