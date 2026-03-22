using System.ComponentModel;
using System.Runtime.CompilerServices;

using Yndx.Services;

namespace Yndx.Models;

public sealed class SearchResultItem : INotifyPropertyChanged
{
    public SearchResultItem()
    {
        LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;
    }

    public required SearchScope Kind { get; init; }

    public required string Title { get; init; }

    public string Subtitle { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool CanDownload { get; init; }

    public string ActionLabel => CanDownload
        ? LocalizationService.Instance["ActionDownload"]
        : LocalizationService.Instance["ActionBrowse"];

    public string OpenLabel => LocalizationService.Instance["ResultOpen"];

    public string ActionBrush => CanDownload ? "#A64B2A" : "#7B8471";

    public required object RawItem { get; init; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LocalizationService.CurrentLanguage) or "Item[]")
        {
            RaisePropertyChanged(nameof(ActionLabel));
            RaisePropertyChanged(nameof(OpenLabel));
        }
    }

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
