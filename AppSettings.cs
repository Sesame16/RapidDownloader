using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Rapid_Downloader
{
	public class CategoryConfig
	{
		public string Name { get; set; } = string.Empty;
		public string Extensions { get; set; } = string.Empty;
		public bool IsDefault { get; set; } = false;

		public CategoryConfig() { }

		public CategoryConfig(string name, string extensions, bool isDefault = false)
		{
			Name = name;
			Extensions = extensions;
			IsDefault = isDefault;
		}

		public bool Matches(string extension)
		{
			if (string.IsNullOrWhiteSpace(Extensions) || string.IsNullOrWhiteSpace(extension))
				return false;

			string cleanExt = extension.TrimStart('.').ToLowerInvariant();
			var tokens = Extensions.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
								   .Select(t => t.Trim().TrimStart('.').ToLowerInvariant());

			return tokens.Contains(cleanExt);
		}
	}

	public class ServerConnectionException
	{
		public string ServerPattern { get; set; } = string.Empty;
		public int Connections { get; set; } = 4;

		public ServerConnectionException() { }

		public ServerConnectionException(string serverPattern, int connections)
		{
			ServerPattern = serverPattern;
			Connections = connections;
		}

		public bool MatchesHost(string host)
		{
			if (string.IsNullOrWhiteSpace(ServerPattern) || string.IsNullOrWhiteSpace(host))
				return false;

			string pattern = ServerPattern.Trim().ToLowerInvariant();
			string target = host.Trim().ToLowerInvariant();

			if (pattern.StartsWith("*."))
			{
				string suffix = pattern.Substring(2);
				return target.Equals(suffix, StringComparison.OrdinalIgnoreCase) ||
					   target.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase);
			}

			return target.Equals(pattern, StringComparison.OrdinalIgnoreCase);
		}
	}

	public class CustomDnsConfig
	{
		public string Name { get; set; } = "Custom DNS";
		public string PrimaryIp { get; set; } = "1.1.1.1";
		public string SecondaryIp { get; set; } = "1.0.0.1";
		public string DohUrl { get; set; } = string.Empty;
	}

	public record DnsPreset(string DisplayName, string PrimaryIp, string SecondaryIp, string DohUrl);

	public static class DnsCatalog
	{
		public const string SystemDnsName = "The system's DNS";
		public const string CustomDnsName = "Another DNS...";

		public static readonly List<DnsPreset> Presets = new()
		{
			new(SystemDnsName, "", "", ""),
			new("Cloudflare (1.1.1.1 / 1.0.0.1)", "1.1.1.1", "1.0.0.1", "https://cloudflare-dns.com/dns-query"),
			new("Cloudflare Family — Malware & Adult Block (1.1.1.3)", "1.1.1.3", "1.0.0.3", "https://family.cloudflare-dns.com/dns-query"),
			new("Google Public DNS (8.8.8.8 / 8.8.4.4)", "8.8.8.8", "8.8.4.4", "https://dns.google/dns-query"),
			new("Quad9 Security (9.9.9.9 / 149.112.112.112)", "9.9.9.9", "149.112.112.112", "https://dns.quad9.net/dns-query"),
			new("AdGuard DNS — Ad & Tracker Block (94.140.14.14)", "94.140.14.14", "94.140.15.15", "https://dns.adguard-dns.com/dns-query"),
			new("NextDNS (45.90.28.0 / 45.90.30.0)", "45.90.28.0", "45.90.30.0", "https://dns.nextdns.io"),
			new("OpenDNS Home (208.67.222.222 / 208.67.220.220)", "208.67.222.222", "208.67.220.220", "https://doh.opendns.com/dns-query"),
			new("CleanBrowsing Security (185.228.168.9)", "185.228.168.9", "185.228.169.9", "https://doh.cleanbrowsing.org/doh/security-filter/"),
			new("Mullvad DNS — No-Logging (194.242.2.2)", "194.242.2.2", "", "https://dns.mullvad.net/dns-query"),
			new("Control D (76.76.2.0 / 76.76.10.0)", "76.76.2.0", "76.76.10.0", "https://freedns.controld.com/p0"),
			new(CustomDnsName, "", "", "")
		};

		public static DnsPreset? FindPreset(string displayName)
		{
			return Presets.FirstOrDefault(p => p.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));
		}
	}

	public class AppSettings
	{
		private static readonly string SettingsFolder = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RapidDownloader");

		private static readonly string SettingsFilePath = Path.Combine(SettingsFolder, "settings.json");

		public static AppSettings Current { get; private set; } = Load();

		// Save File Settings
		public List<CategoryConfig> Categories { get; set; } = new();
		public string TempDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "RapidDownloader");
		public bool SetFileCreationDateFromServer { get; set; } = false;

		// Segments & Connections Settings
		public int DefaultMaxConnections { get; set; } = 8;
		public bool AllowMoreThan32Connections { get; set; } = false;
		public List<ServerConnectionException> ServerExceptions { get; set; } = new();

		// Download Quota Limits
		public bool EnableDownloadLimits { get; set; } = false;
		public int DownloadLimitMBytes { get; set; } = 200;
		public int DownloadLimitHours { get; set; } = 5;
		public bool WarnBeforeStoppingLimit { get; set; } = true;

		// DNS Configuration
		public string SelectedDnsProvider { get; set; } = DnsCatalog.SystemDnsName;
		public CustomDnsConfig CustomDns { get; set; } = new();

		public static AppSettings Load()
		{
			try
			{
				if (File.Exists(SettingsFilePath))
				{
					string json = File.ReadAllText(SettingsFilePath);
					var settings = JsonSerializer.Deserialize<AppSettings>(json);
					if (settings != null)
					{
						if (settings.Categories.Count == 0)
							settings.InitializeDefaultCategories();

						if (string.IsNullOrWhiteSpace(settings.TempDirectory))
							settings.TempDirectory = Path.Combine(Path.GetTempPath(), "RapidDownloader");

						settings.CustomDns ??= new CustomDnsConfig();
						settings.ServerExceptions ??= new List<ServerConnectionException>();
						return settings;
					}
				}
			}
			catch (Exception ex)
			{
				AppLogger.Warn($"[Settings] Failed to load settings.json: {ex.Message}. Loading defaults.");
			}

			var defaultSettings = new AppSettings();
			defaultSettings.InitializeDefaultCategories();
			defaultSettings.Save();
			return defaultSettings;
		}

		public void Save()
		{
			try
			{
				Directory.CreateDirectory(SettingsFolder);
				string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
				File.WriteAllText(SettingsFilePath, json);
				AppLogger.Info("[Settings] Settings saved successfully.");
			}
			catch (Exception ex)
			{
				AppLogger.Error("[Settings] Failed to save settings to disk", ex);
			}
		}

		public void InitializeDefaultCategories()
		{
			Categories = new List<CategoryConfig>
			{
				new("Documents", "pdf doc docx xls xlsx ppt pptx txt rtf odt csv", true),
				new("Media", "mp3 wav aac flac mp4 mkv avi mov flv webm jpg jpeg png gif webp", true),
				new("Applications", "exe msi bat cmd apk dmg pkg appimage", true),
				new("Archived", "zip rar 7z tar gz bz2 iso xz cab", true),
				new("Others", "", true)
			};
		}

		public string GetCategoryForExtension(string extension)
		{
			if (string.IsNullOrWhiteSpace(extension)) return "Others";

			foreach (var category in Categories)
			{
				if (category.Name != "Others" && category.Matches(extension))
				{
					return category.Name;
				}
			}

			return "Others";
		}

		public int GetConnectionsForUrl(string url)
		{
			int maxAllowed = AllowMoreThan32Connections ? 64 : 32;
			int conn = Math.Clamp(DefaultMaxConnections, 1, maxAllowed);

			if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && uri != null)
			{
				var exception = ServerExceptions.FirstOrDefault(e => e.MatchesHost(uri.Host));
				if (exception != null)
				{
					conn = Math.Clamp(exception.Connections, 1, maxAllowed);
				}
			}

			return conn;
		}

		public string GetActiveDohTemplate()
		{
			if (SelectedDnsProvider == DnsCatalog.SystemDnsName)
				return string.Empty;

			if (SelectedDnsProvider == DnsCatalog.CustomDnsName)
				return CustomDns?.DohUrl?.Trim() ?? string.Empty;

			var preset = DnsCatalog.FindPreset(SelectedDnsProvider);
			return preset?.DohUrl ?? string.Empty;
		}
	}
}