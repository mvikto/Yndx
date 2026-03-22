using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

using Yandex.Music.Api.Models.Track;

using Yndx.Models;
using Yndx.Services;

namespace Yndx.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly AppSettingsStore _settingsStore = new();
    private readonly YandexMusicService _musicService = new();
    private readonly LocalizationService _localization = LocalizationService.Instance;
    private readonly ObservableCollection<DownloadQueueItem> _queue = [];
    private readonly ObservableCollection<SearchResultItem> _results = [];
    private readonly ObservableCollection<SearchScopeOption> _searchScopes = [];
    private readonly ObservableCollection<LanguageOption> _languageOptions = [];

    private bool _queueRunning;
    private string _token = string.Empty;
    private string _downloadFolder = string.Empty;
    private string _searchText = string.Empty;
    private SearchScope _selectedScope = SearchScope.Track;
    private SearchScopeOption? _selectedScopeOption;
    private LanguageOption? _selectedLanguageOption;
    private bool _isBusy;
    private bool _isConnected;
    private string _statusMessage = string.Empty;
    private string _accountDisplay = string.Empty;
    private SearchResultItem? _selectedResult;
    private EntityDetail? _detail;

    public MainViewModel()
    {
        _localization.PropertyChanged += OnLocalizationChanged;
        _accountDisplay = T("StatusNotConnected");
        _statusMessage = T("StatusEnterToken");
        RebuildOptionCollections();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string MainWindowTitle => T("MainWindowTitle");

    public string SessionSectionTitle => T("SessionSectionTitle");

    public string SessionHint => T("SessionHint");

    public string DownloadFolderWatermark => T("DownloadFolder");

    public string LanguageLabel => T("LanguageLabel");

    public string NoteBrowseOnly => T("NoteBrowseOnly");

    public string SearchPlaceholder => T("SearchPlaceholder");

    public string SearchButtonText => T("SearchButton");

    public string ResultsSectionTitle => T("ResultsSectionTitle");

    public string ResultsHint => T("ResultsHint");

    public string QueueSectionTitle => T("QueueSectionTitle");

    public string QueueHint => T("QueueRunHint");

    public string DetailsSectionTitle => T("DetailsSectionTitle");

    public string DetailInspectHint => T("DetailInspectHint");

    public string DownloadAllText => T("DownloadAll");

    public string Token
    {
        get => _token;
        set
        {
            if (SetField(ref _token, value))
            {
                RaisePropertyChanged(nameof(HasSavedToken));
                RaisePropertyChanged(nameof(TokenButtonText));
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

    public ObservableCollection<SearchScopeOption> SearchScopes => _searchScopes;

    public SearchScopeOption? SelectedScopeOption
    {
        get => _selectedScopeOption;
        set
        {
            if (SetField(ref _selectedScopeOption, value) && value is not null)
            {
                _selectedScope = value.Scope;
                _ = PersistSettingsAsync();
            }
        }
    }

    public ObservableCollection<LanguageOption> LanguageOptions => _languageOptions;

    public LanguageOption? SelectedLanguageOption
    {
        get => _selectedLanguageOption;
        set
        {
            if (SetField(ref _selectedLanguageOption, value) && value is not null)
            {
                _localization.SetLanguage(value.Language);
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

    public bool HasSavedToken => !string.IsNullOrWhiteSpace(Token);

    public string TokenButtonText => HasSavedToken ? T("SavedTokenChange") : T("SetToken");

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

    public IReadOnlyList<TrackEntry> DetailTracks => Detail?.Tracks ?? [];

    public string DetailTitle => Detail?.Title ?? T("DetailEmptyPickResult");

    public string DetailSubtitle => Detail?.Subtitle ?? string.Empty;

    public string DetailDescription => Detail?.Description ?? string.Empty;

    public bool CanDownloadDetail => Detail?.CanDownloadAll == true;

    public bool IsDetailEmpty => Detail is null || Detail.Tracks.Count == 0;

    public string DetailEmptyMessage => Detail is null
        ? T("DetailEmptyPickResult")
        : Detail.IsBrowseOnly
            ? T("DetailEmptyBrowseOnly")
            : T("DetailEmptyNoTracks");

    public async Task InitializeAsync()
    {
        var settings = await _settingsStore.LoadAsync();

        _localization.Initialize(settings.Language);
        _token = settings.Token;
        _downloadFolder = string.IsNullOrWhiteSpace(settings.DownloadFolder)
            ? BuildDefaultDownloadFolder()
            : settings.DownloadFolder;
        _selectedScope = settings.SearchScope;
        _accountDisplay = T("StatusNotConnected");

        RebuildOptionCollections();

        RaisePropertyChanged(nameof(Token));
        RaisePropertyChanged(nameof(DownloadFolder));
        RaisePropertyChanged(nameof(HasSavedToken));
        RaisePropertyChanged(nameof(TokenButtonText));

        StatusMessage = string.IsNullOrWhiteSpace(Token)
            ? T("StatusEnterToken")
            : T("StatusChooseSavedToken");
    }

    public async Task<bool> ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            StatusMessage = T("StatusEnterToken");
            return false;
        }

        var success = false;

        await RunBusyAsync(async () =>
        {
            var account = await _musicService.ConnectAsync(Token);
            IsConnected = true;
            AccountDisplay = string.IsNullOrWhiteSpace(account.DisplayName) ? account.Login : account.DisplayName;
            StatusMessage = _localization.Format("StatusConnectedAs", AccountDisplay);
            success = true;
            await PersistSettingsAsync();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                await SearchAsync();
            }
        });

        if (!success)
        {
            await _musicService.DisconnectAsync();
            IsConnected = false;
            AccountDisplay = T("StatusNotConnected");
        }

        return success;
    }

    public async Task<bool> SaveTokenAndConnectAsync(string token)
    {
        Token = token.Trim();
        return await ConnectAsync();
    }

    public async Task SearchAsync()
    {
        if (!EnsureConnected())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            StatusMessage = T("StatusSearchPrompt");
            return;
        }

        await RunBusyAsync(async () =>
        {
            var items = await _musicService.SearchAsync(SearchText.Trim(), _selectedScope);

            _results.Clear();
            foreach (var item in items)
            {
                _results.Add(item);
            }

            SelectedResult = null;
            Detail = null;
            StatusMessage = _results.Count == 0
                ? _localization.Format("StatusNoResults", GetScopeLabel(_selectedScope).ToLower(CultureInfo.CurrentCulture))
                : _localization.Format("StatusResultsLoaded", _results.Count);
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
                ? T("StatusLoadedBrowseOnly")
                : _localization.Format("StatusLoadedDetails", item.Title);
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

        StatusMessage = T("StatusAddedQueue");

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
        var folderHint = Detail?.FolderHint ?? T("YandexFolderTracks");
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
            foreach (var item in _queue.Where(queueItem => queueItem.Status == DownloadStatus.Pending))
            {
                item.Status = DownloadStatus.Downloading;
                item.Error = string.Empty;
                try
                {
                    item.TargetPath = await _musicService.DownloadTrackAsync(item.Track, DownloadFolder, item.FolderHint);
                    item.Status = DownloadStatus.Done;
                    StatusMessage = _localization.Format("StatusDownloaded", item.Title);
                }
                catch (Exception ex)
                {
                    item.Status = DownloadStatus.Failed;
                    item.Error = ex.Message;
                    StatusMessage = _localization.Format("StatusDownloadFailed", item.Title);
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

        StatusMessage = T("StatusConnectFirst");
        return false;
    }

    private Task PersistSettingsAsync()
    {
        return _settingsStore.SaveAsync(new AppSettings
        {
            Token = Token,
            DownloadFolder = DownloadFolder,
            SearchScope = _selectedScope,
            Language = _localization.CurrentLanguage
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

    private void RebuildOptionCollections()
    {
        _searchScopes.Clear();
        foreach (var scope in Enum.GetValues<SearchScope>())
        {
            _searchScopes.Add(new SearchScopeOption
            {
                Scope = scope,
                Label = GetScopeLabel(scope)
            });
        }

        _languageOptions.Clear();
        _languageOptions.Add(new LanguageOption { Language = AppLanguage.English, Label = T("LanguageEnglish") });
        _languageOptions.Add(new LanguageOption { Language = AppLanguage.Russian, Label = T("LanguageRussian") });

        _selectedScopeOption = _searchScopes.FirstOrDefault(item => item.Scope == _selectedScope) ?? _searchScopes.FirstOrDefault();
        _selectedLanguageOption = _languageOptions.FirstOrDefault(item => item.Language == _localization.CurrentLanguage) ?? _languageOptions.FirstOrDefault();

        RaisePropertyChanged(nameof(SelectedScopeOption));
        RaisePropertyChanged(nameof(SelectedLanguageOption));
    }

    private string GetScopeLabel(SearchScope scope)
    {
        return scope switch
        {
            SearchScope.Track => T("ScopeTrack"),
            SearchScope.Album => T("ScopeAlbum"),
            SearchScope.Artist => T("ScopeArtist"),
            SearchScope.Playlist => T("ScopePlaylist"),
            SearchScope.PodcastEpisode => T("ScopePodcastEpisode"),
            SearchScope.Video => T("ScopeVideo"),
            SearchScope.User => T("ScopeUser"),
            _ => scope.ToString()
        };
    }

    private string T(string key)
    {
        return _localization[key];
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(LocalizationService.CurrentLanguage) or "Item[]"))
        {
            return;
        }

        RebuildOptionCollections();

        if (!IsConnected)
        {
            AccountDisplay = T("StatusNotConnected");
        }

        RaisePropertyChanged(nameof(MainWindowTitle));
        RaisePropertyChanged(nameof(SessionSectionTitle));
        RaisePropertyChanged(nameof(SessionHint));
        RaisePropertyChanged(nameof(DownloadFolderWatermark));
        RaisePropertyChanged(nameof(LanguageLabel));
        RaisePropertyChanged(nameof(NoteBrowseOnly));
        RaisePropertyChanged(nameof(SearchPlaceholder));
        RaisePropertyChanged(nameof(SearchButtonText));
        RaisePropertyChanged(nameof(ResultsSectionTitle));
        RaisePropertyChanged(nameof(ResultsHint));
        RaisePropertyChanged(nameof(QueueSectionTitle));
        RaisePropertyChanged(nameof(QueueHint));
        RaisePropertyChanged(nameof(DetailsSectionTitle));
        RaisePropertyChanged(nameof(DetailInspectHint));
        RaisePropertyChanged(nameof(DownloadAllText));
        RaisePropertyChanged(nameof(TokenButtonText));
        RaisePropertyChanged(nameof(DetailTitle));
        RaisePropertyChanged(nameof(DetailEmptyMessage));

        if (string.IsNullOrWhiteSpace(Token) && !IsConnected)
        {
            StatusMessage = T("StatusEnterToken");
        }
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
