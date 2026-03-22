using Yandex.Music.Api.Models.Track;

namespace Yndx.Models;

public sealed class DownloadQueueItem
{
    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public required string FolderHint { get; init; }

    public required YTrack Track { get; init; }

    public string Status { get; set; } = "Pending";

    public string TargetPath { get; set; } = string.Empty;

    public string Error { get; set; } = string.Empty;
}
