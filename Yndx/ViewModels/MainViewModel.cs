using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

using Yandex.Music.Api.Models.Track;

using Yndx.Models;
using Yndx.Services;

namespace Yndx.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly AppSettingsStore _settingsStore = new();
    private readonly YandexMusicService _musicService = new();
    private readonly ObservableCollection<DownloadQueueItem> _queue = [];
    private readonly ObservableCollection<SearchResultItem> _results = [];

    private bool _queueRunning;
    private string _token = string.Empty;
    private string _downloadFolder = string.Empty;
    private string _searchText = string.Empty;
    private SearchScope _selectedScope = SearchScope.Track;
    private bool _isBusy;
    private bool _isConnected;
    private string _statusMessage = "Paste a token to connect.";
    private string _accountDisplay = "Not connected";
    private SearchResultItem? _selectedResult;
    private EntityDetail? _detail;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Token
    {
        get => _token;
        set
        {
            if (SetField(ref _token, value))
            {
                _ = PersistSettingsAsync();
            }
        }
    }

    public string DownloadFolder
    {
        get => _downloadFolder;
        set
        {
            if (SetField(ref _downloadFolder, value))
            {
                _ = PersistSettingsAsync();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set => SetField(ref _searchText, value);
    }

    public SearchScope SelectedScope
    {
        get => _selectedScope;
        set
        {
            if (SetField(ref _selectedScope, value))
            {
                _ = PersistSettingsAsync();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                RaisePropertyChanged(nameof(NotBusy));
                RaisePropertyChanged(nameof(CanSearch));
            }
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (SetField(ref _isConnected, value))
            {
                RaisePropertyChanged(nameof(ConnectButtonText));
                RaisePropertyChanged(nameof(CanSearch));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public string AccountDisplay
    {
        get => _accountDisplay;
        private set => SetField(ref _accountDisplay, value);
    }

    public string ConnectButtonText => IsConnected ? "Reconnect" : "Connect";

    public bool NotBusy => !IsBusy;

    public bool CanSearch => NotBusy && IsConnected;

    public ObservableCollection<SearchResultItem> Results => _results;

    public SearchResultItem? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (SetField(ref _selectedResult, value) && value is not null)
            {
                _ = SelectResultAsync(value);
            }
        }
    }

    public EntityDetail? Detail
    {
        get => _detail;
        private set
        {
            if (SetField(ref _detail, value))
            {
                RaisePropertyChanged(nameof(DetailTracks));
                RaisePropertyChanged(nameof(DetailTitle));
                RaisePropertyChanged(nameof(DetailSubtitle));
                RaisePropertyChanged(nameof(DetailDescription));
                RaisePropertyChanged(nameof(CanDownloadDetail));
                RaisePropertyChanged(nameof(IsDetailEmpty));
                RaisePropertyChanged(nameof(DetailEmptyMessage));
            }
        }
    }

    public ObservableCollection<DownloadQueueItem> Queue => _queue;

    public IEnumerable<SearchScope> SearchScopes => Enum.GetValues<SearchScope>();

    public IReadOnlyList<TrackEntry> DetailTracks => Detail?.Tracks ?? [];

    public string DetailTitle => Detail?.Title ?? "Pick a result to inspect tracks or browse-only details.";

    public string DetailSubtitle => Detail?.Subtitle ?? string.Empty;

    public string DetailDescription => Detail?.Description ?? string.Empty;

    public bool CanDownloadDetail => Detail?.CanDownloadAll == true;

    public bool IsDetailEmpty => Detail is null || Detail.Tracks.Count == 0;

    public string DetailEmptyMessage => Detail is null
        ? "Pick a result to inspect tracks or browse-only details."
        : Detail.IsBrowseOnly
            ? "This result can be browsed but not downloaded."
            : "No tracks were returned.";

    public async Task InitializeAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        _token = settings.Token;
        _downloadFolder = string.IsNullOrWhiteSpace(settings.DownloadFolder)
            ? BuildDefaultDownloadFolder()
            : settings.DownloadFolder;
        _selectedScope = settings.SearchScope;

        RaisePropertyChanged(nameof(Token));
        RaisePropertyChanged(nameof(DownloadFolder));
        RaisePropertyChanged(nameof(SelectedScope));

        if (!string.IsNullOrWhiteSpace(Token))
        {
            StatusMessage = "Found a saved token. Connecting...";
            await ConnectAsync();
        }
    }

    public async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            StatusMessage = "Paste a Yandex token first.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var account = await _musicService.ConnectAsync(Token);
            IsConnected = true;
            AccountDisplay = string.IsNullOrWhiteSpace(account.DisplayName) ? account.Login : account.DisplayName;
            StatusMessage = $"Connected as {AccountDisplay}.";
            await PersistSettingsAsync();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                await SearchAsync();
            }
        });
    }

    public async Task SearchAsync()
    {
        if (!EnsureConnected())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            StatusMessage = "Type something to search.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var items = await _musicService.SearchAsync(SearchText.Trim(), SelectedScope);

            _results.Clear();
            foreach (var item in items)
            {
                _results.Add(item);
            }

            SelectedResult = null;
            Detail = null;
            StatusMessage = _results.Count == 0
                ? $"No {SelectedScope.ToString().ToLowerInvariant()} results found."
                : $"Loaded {_results.Count} result(s).";
        });
    }

    public async Task SelectResultAsync(SearchResultItem item)
    {
        if (!EnsureConnected())
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            Detail = await _musicService.LoadDetailAsync(item);
            StatusMessage = Detail.IsBrowseOnly
                ? "This result is browse-only."
                : $"Loaded details for {item.Title}.";
        });
    }

    public async Task DownloadResultAsync(SearchResultItem item)
    {
        await SelectResultAsync(item);
        if (Detail is null || !Detail.CanDownloadAll)
        {
            return;
        }

        await QueueTracksAsync(Detail.Tracks, Detail.FolderHint);
    }

    public Task QueueTracksAsync(IEnumerable<TrackEntry> tracks, string folderHint)
    {
        foreach (var track in tracks)
        {
            _queue.Add(new DownloadQueueItem
            {
                Title = track.Title,
                Subtitle = track.Subtitle,
                FolderHint = folderHint,
                Track = track.Track
            });
        }

        StatusMessage = "Added items to the download queue.";

        if (!_queueRunning)
        {
            _ = ProcessQueueAsync();
        }

        return Task.CompletedTask;
    }

    public Task QueueSingleTrackAsync(YTrack track, string folderHint)
    {
        return QueueTracksAsync(
        [
            new TrackEntry
            {
                Track = track,
                Title = track.Title,
                Subtitle = string.Join(", ", track.Artists?.Select(item => item.Name) ?? []),
                Album = track.Albums?.FirstOrDefault()?.Title ?? string.Empty
            }
        ], folderHint);
    }

    public Task QueueTrackEntryAsync(TrackEntry track)
    {
        var folderHint = Detail?.FolderHint ?? "Tracks";
        return QueueSingleTrackAsync(track.Track, folderHint);
    }

    public Task QueueCurrentDetailAsync()
    {
        if (Detail is null || !Detail.CanDownloadAll)
        {
            return Task.CompletedTask;
        }

        return QueueTracksAsync(Detail.Tracks, Detail.FolderHint);
    }

    private async Task ProcessQueueAsync()
    {
        _queueRunning = true;

        try
        {
            foreach (var item in _queue.Where(queueItem => queueItem.Status == "Pending"))
            {
                item.Status = "Downloading";
                item.Error = string.Empty;
                try
                {
                    item.TargetPath = await _musicService.DownloadTrackAsync(item.Track, DownloadFolder, item.FolderHint);
                    item.Status = "Done";
                    StatusMessage = $"Downloaded {item.Title}.";
                }
                catch (Exception ex)
                {
                    item.Status = "Failed";
                    item.Error = ex.Message;
                    StatusMessage = $"Failed to download {item.Title}.";
                }

            }
        }
        finally
        {
            _queueRunning = false;
        }
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool EnsureConnected()
    {
        if (IsConnected)
        {
            return true;
        }

        StatusMessage = "Connect with your token first.";
        return false;
    }

    private Task PersistSettingsAsync()
    {
        return _settingsStore.SaveAsync(new AppSettings
        {
            Token = Token,
            DownloadFolder = DownloadFolder,
            SearchScope = SelectedScope
        });
    }

    private static string BuildDefaultDownloadFolder()
    {
        var musicDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        if (string.IsNullOrWhiteSpace(musicDirectory))
        {
            musicDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return Path.Combine(musicDirectory, "Yndx Downloads");
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        RaisePropertyChanged(propertyName);
        return true;
    }

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
