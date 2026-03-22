namespace Yndx.Models;

public sealed class SearchResultItem
{
    public required SearchScope Kind { get; init; }

    public required string Title { get; init; }

    public string Subtitle { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool CanDownload { get; init; }

    public string ActionLabel => CanDownload ? "Download" : "Browse";

    public string ActionBrush => CanDownload ? "#A64B2A" : "#7B8471";

    public required object RawItem { get; init; }
}
