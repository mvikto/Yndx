namespace Yndx.Models;

public sealed class EntityDetail
{
    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public string Description { get; init; } = string.Empty;

    public string FolderHint { get; init; } = string.Empty;

    public bool CanDownloadAll { get; init; }

    public bool IsBrowseOnly { get; init; }

    public IReadOnlyList<TrackEntry> Tracks { get; init; } = [];
}
