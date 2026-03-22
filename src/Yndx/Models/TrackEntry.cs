using System.ComponentModel;
using System.Runtime.CompilerServices;

using Yandex.Music.Api.Models.Track;

using Yndx.Services;

namespace Yndx.Models;

public sealed class TrackEntry : INotifyPropertyChanged
{
    public TrackEntry()
    {
        LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;
    }

    public required YTrack Track { get; init; }

    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public string Album { get; init; } = string.Empty;

    public string QueueLabel => LocalizationService.Instance["QueueTrack"];

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LocalizationService.CurrentLanguage) or "Item[]")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QueueLabel)));
        }
    }
}
