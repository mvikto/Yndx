using Yandex.Music.Api.Models.Track;

namespace Yndx.Utilities;

public static class FileNameSanitizer
{
    public static string BuildTrackFileName(YTrack track)
    {
        var artist = SanitizeSegment(string.Join(", ", track.Artists?.Select(item => item.Name).Where(name => !string.IsNullOrWhiteSpace(name)) ?? []));
        var title = SanitizeSegment(track.Title);

        var baseName = string.IsNullOrWhiteSpace(artist) ? title : $"{artist} - {title}";
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = $"track-{track.Id}";
        }

        return baseName + ".mp3";
    }

    public static string MakeUnique(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var counter = 2;

        while (true)
        {
            var candidate = Path.Combine(directory, $"{fileName} ({counter}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }

            counter++;
        }
    }

    public static string SanitizeSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Trim().Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return sanitized.Replace(':', '-').Trim();
    }
}
