namespace Rapid_Downloader
{
    public interface IBrowserExtensionBuilder
    {
        /// <summary>
        /// Label displayed on the drag-and-drop card (e.g. "Chromium Browsers", "FireFox").
        /// </summary>
        string BrowserDisplayName { get; }

        /// <summary>
        /// Output package filename generated on disk (e.g. "RapidDownloader.crx", "RapidDownloader.xpi").
        /// </summary>
        string PackageFileName { get; }

        /// <summary>
        /// Short drag-and-drop instruction shown beneath the card title.
        /// </summary>
        string InstallHint { get; }

        /// <summary>
        /// Generates the browser-specific extension package configured with the active bridge port and token,
        /// and returns the full file path ready for FileDrop dragging.
        /// </summary>
        string BuildPackage(AppSettings settings);
    }
}