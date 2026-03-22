using Yandex.Music.Api.Models.Track;

namespace Yndx.Models;

public sealed class TrackEntry
{
    public required YTrack Track { get; init; }

    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public string Album { get; init; } = string.Empty;
}
