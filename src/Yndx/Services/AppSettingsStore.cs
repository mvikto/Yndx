using System.Text;

using Yndx.Models;

namespace Yndx.Services;

public sealed class AppSettingsStore
{
    private readonly string _settingsPath;

    public AppSettingsStore()
    {
        var baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Yndx");

        Directory.CreateDirectory(baseDirectory);
        _settingsPath = Path.Combine(baseDirectory, "settings.txt");
    }

    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        var settings = new AppSettings();
        var lines = await File.ReadAllLinesAsync(_settingsPath, Encoding.UTF8);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split('=', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            var key = parts[0].Trim();
            var value = Uri.UnescapeDataString(parts[1]);

            switch (key)
            {
                case "token":
                    settings.Token = value;
                    break;
                case "downloadFolder":
                    settings.DownloadFolder = value;
                    break;
                case "searchScope" when Enum.TryParse<SearchScope>(value, out var scope):
                    settings.SearchScope = scope;
                    break;
                case "language" when Enum.TryParse<AppLanguage>(value, out var language):
                    settings.Language = language;
                    break;
            }
        }

        return settings;
    }

    public Task SaveAsync(AppSettings settings)
    {
        var lines = new[]
        {
            $"token={Uri.EscapeDataString(settings.Token)}",
            $"downloadFolder={Uri.EscapeDataString(settings.DownloadFolder)}",
            $"searchScope={Uri.EscapeDataString(settings.SearchScope.ToString())}",
            $"language={Uri.EscapeDataString(settings.Language?.ToString() ?? string.Empty)}"
        };

        return File.WriteAllLinesAsync(_settingsPath, lines, Encoding.UTF8);
    }
}
