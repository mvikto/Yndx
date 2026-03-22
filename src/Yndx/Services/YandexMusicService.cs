using Yandex.Music.Api;
using Yandex.Music.Api.Common;
using Yandex.Music.Api.Models.Account;
using Yandex.Music.Api.Models.Album;
using Yandex.Music.Api.Models.Artist;
using Yandex.Music.Api.Models.Common;
using Yandex.Music.Api.Models.Playlist;
using Yandex.Music.Api.Models.Search;
using Yandex.Music.Api.Models.Search.Album;
using Yandex.Music.Api.Models.Search.Artist;
using Yandex.Music.Api.Models.Search.Playlist;
using Yandex.Music.Api.Models.Search.Track;
using Yandex.Music.Api.Models.Search.User;
using Yandex.Music.Api.Models.Search.Video;
using Yandex.Music.Api.Models.Track;

using Yndx.Models;
using Yndx.Utilities;

namespace Yndx.Services;

public sealed class YandexMusicService
{
    private readonly YandexMusicApi _api = new();
    private readonly LocalizationService _localization = LocalizationService.Instance;
    private AuthStorage _storage = new();

    public bool IsAuthorized => _storage.IsAuthorized;

    public async Task<YAccount> ConnectAsync(string token)
    {
        await _api.User.AuthorizeAsync(_storage, token.Trim());
        return _storage.User;
    }

    public Task DisconnectAsync()
    {
        _storage = new AuthStorage();
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<SearchResultItem>> SearchAsync(string query, SearchScope scope)
    {
        EnsureAuthorized();

        YResponse<YSearch> response = scope switch
        {
            SearchScope.Track => await _api.Search.TrackAsync(_storage, query),
            SearchScope.Album => await _api.Search.AlbumsAsync(_storage, query),
            SearchScope.Artist => await _api.Search.ArtistAsync(_storage, query),
            SearchScope.Playlist => await _api.Search.PlaylistAsync(_storage, query),
            SearchScope.PodcastEpisode => await _api.Search.PodcastEpisodeAsync(_storage, query),
            SearchScope.Video => await _api.Search.VideosAsync(_storage, query),
            SearchScope.User => await _api.Search.UsersAsync(_storage, query),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
        };

        return scope switch
        {
            SearchScope.Track => response.Result.Tracks?.Results?.Select(ToTrackSearchResult).ToList() ?? [],
            SearchScope.Album => response.Result.Albums?.Results?.Select(ToAlbumSearchResult).ToList() ?? [],
            SearchScope.Artist => response.Result.Artists?.Results?.Select(ToArtistSearchResult).ToList() ?? [],
            SearchScope.Playlist => response.Result.Playlists?.Results?.Select(ToPlaylistSearchResult).ToList() ?? [],
            SearchScope.PodcastEpisode => response.Result.PodcastEpisode?.Results?.Select(ToPodcastSearchResult).ToList() ?? [],
            SearchScope.Video => response.Result.Videos?.Results?.Select(ToVideoSearchResult).ToList() ?? [],
            SearchScope.User => response.Result.Users?.Results?.Select(ToUserSearchResult).ToList() ?? [],
            _ => []
        };
    }

    public async Task<EntityDetail> LoadDetailAsync(SearchResultItem item)
    {
        EnsureAuthorized();

        return item.Kind switch
        {
            SearchScope.Track => await BuildTrackDetailAsync((YSearchTrackModel)item.RawItem, false),
            SearchScope.PodcastEpisode => await BuildTrackDetailAsync((YSearchTrackModel)item.RawItem, true),
            SearchScope.Album => await BuildAlbumDetailAsync((YSearchAlbumModel)item.RawItem),
            SearchScope.Artist => await BuildArtistDetailAsync((YSearchArtistModel)item.RawItem),
            SearchScope.Playlist => await BuildPlaylistDetailAsync((YSearchPlaylistModel)item.RawItem),
            SearchScope.Video => BuildVideoDetail((YSearchVideoModel)item.RawItem),
            SearchScope.User => BuildUserDetail((YSearchUserModel)item.RawItem),
            _ => throw new ArgumentOutOfRangeException(nameof(item.Kind), item.Kind, null)
        };
    }

    public async Task<string> DownloadTrackAsync(YTrack track, string downloadFolder, string folderHint)
    {
        EnsureAuthorized();

        var targetDirectory = Path.Combine(downloadFolder, folderHint);
        Directory.CreateDirectory(targetDirectory);

        var fileName = FileNameSanitizer.BuildTrackFileName(track);
        var targetPath = FileNameSanitizer.MakeUnique(Path.Combine(targetDirectory, fileName));

        await _api.Track.ExtractToFileAsync(_storage, track, targetPath);
        return targetPath;
    }

    private SearchResultItem ToTrackSearchResult(YSearchTrackModel track)
    {
        return new SearchResultItem
        {
            Kind = SearchScope.Track,
            Title = track.Title,
            Subtitle = BuildTrackSubtitle(track.Artists),
            Description = track.Albums?.FirstOrDefault()?.Title ?? T("YandexTrack"),
            CanDownload = true,
            RawItem = track
        };
    }

    private SearchResultItem ToPodcastSearchResult(YSearchTrackModel track)
    {
        return new SearchResultItem
        {
            Kind = SearchScope.PodcastEpisode,
            Title = track.Title,
            Subtitle = BuildTrackSubtitle(track.Artists),
            Description = track.ShortDescription ?? T("YandexPodcastEpisode"),
            CanDownload = true,
            RawItem = track
        };
    }

    private SearchResultItem ToAlbumSearchResult(YSearchAlbumModel album)
    {
        return new SearchResultItem
        {
            Kind = SearchScope.Album,
            Title = album.Title,
            Subtitle = JoinArtistNames(album.Artists),
            Description = _localization.Format("YandexAlbumDescription", album.TrackCount, FormatYear(album.Year)),
            CanDownload = true,
            RawItem = album
        };
    }

    private SearchResultItem ToArtistSearchResult(YSearchArtistModel artist)
    {
        return new SearchResultItem
        {
            Kind = SearchScope.Artist,
            Title = artist.Name,
            Subtitle = string.Join(", ", artist.Genres?.Take(3) ?? []),
            Description = artist.Description?.Text ?? T("YandexArtistFallback"),
            CanDownload = true,
            RawItem = artist
        };
    }

    private SearchResultItem ToPlaylistSearchResult(YSearchPlaylistModel playlist)
    {
        var owner = playlist.Owner?.Name ?? playlist.Owner?.Login ?? playlist.Owner?.Uid ?? T("YandexPlaylistFallback");

        return new SearchResultItem
        {
            Kind = SearchScope.Playlist,
            Title = playlist.Title,
            Subtitle = owner,
            Description = _localization.Format("YandexPlaylistTrackCount", playlist.TrackCount),
            CanDownload = true,
            RawItem = playlist
        };
    }

    private SearchResultItem ToVideoSearchResult(YSearchVideoModel video)
    {
        return new SearchResultItem
        {
            Kind = SearchScope.Video,
            Title = video.Title,
            Subtitle = T("YandexBrowseOnly"),
            Description = video.Text ?? string.Empty,
            CanDownload = false,
            RawItem = video
        };
    }

    private SearchResultItem ToUserSearchResult(YSearchUserModel user)
    {
        return new SearchResultItem
        {
            Kind = SearchScope.User,
            Title = T("YandexUserResult"),
            Subtitle = T("YandexBrowseOnly"),
            Description = T("YandexUserDescription"),
            CanDownload = false,
            RawItem = user
        };
    }

    private async Task<EntityDetail> BuildTrackDetailAsync(YSearchTrackModel searchTrack, bool isPodcast)
    {
        var track = (await _api.Track.GetAsync(_storage, searchTrack.Id)).Result.FirstOrDefault()
            ?? throw new InvalidOperationException(T("YandexTrackDetailMissing"));

        return new EntityDetail
        {
            Title = track.Title,
            Subtitle = BuildTrackSubtitle(track.Artists),
            Description = isPodcast
                ? track.ShortDescription ?? T("YandexPodcastEpisode")
                : track.Albums?.FirstOrDefault()?.Title ?? T("YandexTrack"),
            FolderHint = isPodcast ? T("YandexFolderPodcastEpisodes") : T("YandexFolderTracks"),
            CanDownloadAll = true,
            IsBrowseOnly = false,
            Tracks = [ToTrackEntry(track)]
        };
    }

    private async Task<EntityDetail> BuildAlbumDetailAsync(YSearchAlbumModel searchAlbum)
    {
        var album = (await _api.Album.GetAsync(_storage, searchAlbum.Id)).Result;
        var tracks = FlattenAlbumTracks(album);

        return new EntityDetail
        {
            Title = album.Title,
            Subtitle = JoinArtistNames(album.Artists),
            Description = album.Description ?? _localization.Format("YandexAlbumDescription", tracks.Count, FormatYear(album.Year)),
            FolderHint = Path.Combine(T("YandexFolderAlbums"), FileNameSanitizer.SanitizeSegment($"{JoinArtistNames(album.Artists)} - {album.Title}")),
            CanDownloadAll = tracks.Count > 0,
            IsBrowseOnly = false,
            Tracks = tracks.Select(ToTrackEntry).ToList()
        };
    }

    private async Task<EntityDetail> BuildArtistDetailAsync(YSearchArtistModel searchArtist)
    {
        var tracksPage = (await _api.Artist.GetAllTracksAsync(_storage, searchArtist.Id)).Result;
        var tracks = tracksPage.Tracks ?? [];

        return new EntityDetail
        {
            Title = searchArtist.Name,
            Subtitle = string.Join(", ", searchArtist.Genres?.Take(3) ?? []),
            Description = searchArtist.Description?.Text ?? _localization.Format("YandexArtistTracksCount", tracks.Count),
            FolderHint = Path.Combine(T("YandexFolderArtists"), FileNameSanitizer.SanitizeSegment(searchArtist.Name)),
            CanDownloadAll = tracks.Count > 0,
            IsBrowseOnly = false,
            Tracks = tracks.Select(ToTrackEntry).ToList()
        };
    }

    private async Task<EntityDetail> BuildPlaylistDetailAsync(YSearchPlaylistModel searchPlaylist)
    {
        YPlaylist playlist;

        if (!string.IsNullOrWhiteSpace(searchPlaylist.Owner?.Uid) && !string.IsNullOrWhiteSpace(searchPlaylist.Kind))
        {
            playlist = (await _api.Playlist.GetAsync(_storage, searchPlaylist.Owner.Uid, searchPlaylist.Kind)).Result;
        }
        else if (!string.IsNullOrWhiteSpace(searchPlaylist.PlaylistUuid))
        {
            playlist = (await _api.Playlist.GetAsync(_storage, searchPlaylist.PlaylistUuid)).Result;
        }
        else
        {
            throw new InvalidOperationException(T("YandexPlaylistIdentifiersMissing"));
        }

        var tracks = playlist.Tracks?
            .Select(container => container.Track)
            .Where(track => track is not null)
            .Cast<YTrack>()
            .ToList() ?? [];

        var owner = playlist.Owner?.Name ?? playlist.Owner?.Login ?? playlist.Owner?.Uid ?? T("YandexPlaylistFallback");

        return new EntityDetail
        {
            Title = playlist.Title,
            Subtitle = owner,
            Description = playlist.Description ?? _localization.Format("YandexPlaylistTrackCount", tracks.Count),
            FolderHint = Path.Combine(T("YandexFolderPlaylist"), FileNameSanitizer.SanitizeSegment($"{owner} - {playlist.Title}")),
            CanDownloadAll = tracks.Count > 0,
            IsBrowseOnly = false,
            Tracks = tracks.Select(ToTrackEntry).ToList()
        };
    }

    private EntityDetail BuildVideoDetail(YSearchVideoModel video)
    {
        return new EntityDetail
        {
            Title = video.Title,
            Subtitle = T("YandexBrowseOnly"),
            Description = string.IsNullOrWhiteSpace(video.YoutubeUrl)
                ? video.Text ?? T("YandexVideoDetailFallback")
                : $"{video.Text}\n{video.YoutubeUrl}",
            FolderHint = string.Empty,
            CanDownloadAll = false,
            IsBrowseOnly = true,
            Tracks = []
        };
    }

    private EntityDetail BuildUserDetail(YSearchUserModel user)
    {
        return new EntityDetail
        {
            Title = T("YandexUserResult"),
            Subtitle = T("YandexBrowseOnly"),
            Description = T("YandexUserDetailDescription"),
            FolderHint = string.Empty,
            CanDownloadAll = false,
            IsBrowseOnly = true,
            Tracks = []
        };
    }

    private static List<YTrack> FlattenAlbumTracks(YAlbum album)
    {
        return album.Volumes?
            .SelectMany(volume => volume)
            .Where(track => track is not null)
            .Distinct()
            .ToList() ?? [];
    }

    private static TrackEntry ToTrackEntry(YTrack track)
    {
        return new TrackEntry
        {
            Track = track,
            Title = track.Title,
            Subtitle = BuildTrackSubtitle(track.Artists),
            Album = track.Albums?.FirstOrDefault()?.Title ?? string.Empty
        };
    }

    private static string BuildTrackSubtitle(IEnumerable<YArtist>? artists)
    {
        var artistLabel = JoinArtistNames(artists);
        return string.IsNullOrWhiteSpace(artistLabel) ? LocalizationService.Instance["UnknownArtist"] : artistLabel;
    }

    private static string JoinArtistNames(IEnumerable<YArtist>? artists)
    {
        return string.Join(", ", artists?.Select(artist => artist.Name).Where(name => !string.IsNullOrWhiteSpace(name)) ?? []);
    }

    private static string FormatYear(int year)
    {
        return year > 0 ? $" - {year}" : string.Empty;
    }

    private void EnsureAuthorized()
    {
        if (!_storage.IsAuthorized)
        {
            throw new InvalidOperationException(T("YandexConnectFirst"));
        }
    }

    private string T(string key)
    {
        return _localization[key];
    }
}
