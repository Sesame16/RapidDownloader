namespace Rapid_Downloader
{
    public interface IOptionsTab
    {
        /// <summary>
        /// Populates the tab's UI controls from the provided settings instance.
        /// </summary>
        void LoadSettings(AppSettings settings);

        /// <summary>
        /// Validates user inputs on this tab before saving.
        /// </summary>
        bool ValidateSettings(out string errorMessage);

        /// <summary>
        /// Writes the tab's UI values back into the provided settings instance.
        /// </summary>
        void ApplySettings(AppSettings settings);
    }
}