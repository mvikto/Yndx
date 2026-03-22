using System.ComponentModel;
using System.Runtime.CompilerServices;

using Yandex.Music.Api.Models.Track;

using Yndx.Models;
using Yndx.Services;

namespace Yndx.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly AppSettingsStore _settingsStore = new();
    private readonly YandexMusicService _musicService = new();
    private readonly List<DownloadQueueItem> _queue = [];

    private bool _queueRunning;
    private string _token = string.Empty;
    private string _downloadFolder = string.Empty;
    private string _searchText = string.Empty;
    private SearchScope _selectedScope = SearchScope.Track;
    private bool _isBusy;
    private bool _isConnected;
    private string _statusMessage = "Paste a token to connect.";
    private string _accountDisplay = "Not connected";
    private IReadOnlyList<SearchResultItem> _results = [];
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
        private set => SetField(ref _isBusy, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set => SetField(ref _isConnected, value);
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

    public IReadOnlyList<SearchResultItem> Results
    {
        get => _results;
        private set => SetField(ref _results, value);
    }

    public EntityDetail? Detail
    {
        get => _detail;
        private set => SetField(ref _detail, value);
    }

    public IReadOnlyList<DownloadQueueItem> Queue => _queue;

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
            Results = await _musicService.SearchAsync(SearchText.Trim(), SelectedScope);
            Detail = null;
            StatusMessage = Results.Count == 0
                ? $"No {SelectedScope.ToString().ToLowerInvariant()} results found."
                : $"Loaded {Results.Count} result(s).";
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

        RaisePropertyChanged(nameof(Queue));
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

    private async Task ProcessQueueAsync()
    {
        _queueRunning = true;

        try
        {
            foreach (var item in _queue.Where(queueItem => queueItem.Status == "Pending"))
            {
                item.Status = "Downloading";
                item.Error = string.Empty;
                RaisePropertyChanged(nameof(Queue));

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

                RaisePropertyChanged(nameof(Queue));
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
