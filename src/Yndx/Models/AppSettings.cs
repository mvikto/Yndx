namespace Yndx.Models;

public sealed class AppSettings
{
    public string Token { get; set; } = string.Empty;

    public string DownloadFolder { get; set; } = string.Empty;

    public SearchScope SearchScope { get; set; } = SearchScope.Track;

    public AppLanguage? Language { get; set; }
}
