using System.ComponentModel;
using System.Runtime.CompilerServices;

using Yandex.Music.Api.Models.Track;

namespace Yndx.Models;

public sealed class DownloadQueueItem : INotifyPropertyChanged
{
    private string _status = "Pending";
    private string _targetPath = string.Empty;
    private string _error = string.Empty;

    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public required string FolderHint { get; init; }

    public required YTrack Track { get; init; }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public string TargetPath
    {
        get => _targetPath;
        set => SetField(ref _targetPath, value);
    }

    public string Error
    {
        get => _error;
        set => SetField(ref _error, value);
    }

    public string DisplayPath => string.IsNullOrWhiteSpace(TargetPath) ? FolderHint : TargetPath;

    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    public string StatusBrush => Status switch
    {
        "Done" => "#0F766E",
        "Failed" => "#B42318",
        "Downloading" => "#A64B2A",
        _ => "#5C6667"
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        if (propertyName is nameof(TargetPath))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayPath)));
        }

        if (propertyName is nameof(Error))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasError)));
        }

        if (propertyName is nameof(Status))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusBrush)));
        }
    }
}
