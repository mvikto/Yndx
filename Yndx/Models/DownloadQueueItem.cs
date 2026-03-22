using System.ComponentModel;
using System.Runtime.CompilerServices;

using Yandex.Music.Api.Models.Track;

using Yndx.Services;

namespace Yndx.Models;

public sealed class DownloadQueueItem : INotifyPropertyChanged
{
    private DownloadStatus _status = DownloadStatus.Pending;
    private string _targetPath = string.Empty;
    private string _error = string.Empty;

    public DownloadQueueItem()
    {
        LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;
    }

    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public required string FolderHint { get; init; }

    public required YTrack Track { get; init; }

    public DownloadStatus Status
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

    public string StatusText => Status switch
    {
        DownloadStatus.Done => LocalizationService.Instance["QueueStatusDone"],
        DownloadStatus.Failed => LocalizationService.Instance["QueueStatusFailed"],
        DownloadStatus.Downloading => LocalizationService.Instance["QueueStatusDownloading"],
        _ => LocalizationService.Instance["QueueStatusPending"]
    };

    public string StatusBrush => Status switch
    {
        DownloadStatus.Done => "#0F766E",
        DownloadStatus.Failed => "#B42318",
        DownloadStatus.Downloading => "#A64B2A",
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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
        }
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LocalizationService.CurrentLanguage) or "Item[]")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
        }
    }
}
