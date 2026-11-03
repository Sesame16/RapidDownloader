using System;
using System.IO;

namespace Rapid_Downloader
{
    public class DownloadInfo
    {
        public string Url { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long TotalBytes { get; set; }
        public string Category { get; set; } = "Others";
        public string SaveDirectory { get; set; } = string.Empty;
        public string? Cookies { get; set; }
        public string? UserAgent { get; set; }
        public string FullSavePath => Path.Combine(SaveDirectory, FileName);
    }
}