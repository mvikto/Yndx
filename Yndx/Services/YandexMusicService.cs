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

    private static SearchResultItem ToTrackSearchResult(YSearchTrackModel track)
    {
        return new SearchResultItem
        {
            Kind = SearchScope.Track,
            Title = track.Title,
            Subtitle = BuildTrackSubtitle(track.Artists),
            Description = track.Albums?.FirstOrDefault()?.Title ?? "Track",
            CanDownload = true,
            RawItem = track
        };
    }

    private static SearchResultItem ToPodcastSearchResult(YSearchTrackModel track)
    {
        return new SearchResultItem
        {
            Kind = SearchScope.PodcastEpisode,
            Title = track.Title,
            Subtitle = BuildTrackSubtitle(track.Artists),
            Description = track.ShortDescription ?? "Podcast episode",
            CanDownload = true,
            RawItem = track
        };
    }

    private static SearchResultItem ToAlbumSearchResult(YSearchAlbumModel album)
    {
        return new SearchResultItem
        {
            Kind = SearchScope.Album,
            Title = album.Title,
            Subtitle = JoinArtistNames(album.Artists),
            Description = $"{album.TrackCount} tracks{FormatYear(album.Year)}",
            CanDownload = true,
            RawItem = album
        };
    }

    private static SearchResultItem ToArtistSearchResult(YSearchArtistModel artist)
    {
        return new SearchResultItem
        {
            Kind = SearchScope.Artist,
            Title = artist.Name,
            Subtitle = string.Join(", ", artist.Genres?.Take(3) ?? []),
            Description = artist.Description?.Text ?? "Artist",
            CanDownload = true,
            RawItem = artist
        };
    }

    private static SearchResultItem ToPlaylistSearchResult(YSearchPlaylistModel playlist)
    {
        var owner = playlist.Owner?.Name ?? playlist.Owner?.Login ?? playlist.Owner?.Uid ?? "Playlist";

        return new SearchResultItem
        {
            Kind = SearchScope.Playlist,
            Title = playlist.Title,
            Subtitle = owner,
            Description = $"{playlist.TrackCount} tracks",
            CanDownload = true,
            RawItem = playlist
        };
    }

    private static SearchResultItem ToVideoSearchResult(YSearchVideoModel video)
    {
        return new SearchResultItem
        {
            Kind = SearchScope.Video,
            Title = video.Title,
            Subtitle = "Browse only",
            Description = video.Text ?? string.Empty,
            CanDownload = false,
            RawItem = video
        };
    }

    private static SearchResultItem ToUserSearchResult(YSearchUserModel user)
    {
        return new SearchResultItem
        {
            Kind = SearchScope.User,
            Title = "User result",
            Subtitle = "Browse only",
            Description = "The package exposes user search results without rich fields.",
            CanDownload = false,
            RawItem = user
        };
    }

    private async Task<EntityDetail> BuildTrackDetailAsync(YSearchTrackModel searchTrack, bool isPodcast)
    {
        var track = (await _api.Track.GetAsync(_storage, searchTrack.Id)).Result.FirstOrDefault()
            ?? throw new InvalidOperationException("Track details were not returned by the API.");

        return new EntityDetail
        {
            Title = track.Title,
            Subtitle = BuildTrackSubtitle(track.Artists),
            Description = isPodcast
                ? track.ShortDescription ?? "Podcast episode"
                : track.Albums?.FirstOrDefault()?.Title ?? "Track",
            FolderHint = isPodcast ? "Podcast Episodes" : "Tracks",
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
            Description = album.Description ?? $"{tracks.Count} tracks{FormatYear(album.Year)}",
            FolderHint = Path.Combine("Albums", FileNameSanitizer.SanitizeSegment($"{JoinArtistNames(album.Artists)} - {album.Title}")),
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
            Description = searchArtist.Description?.Text ?? $"{tracks.Count} track results",
            FolderHint = Path.Combine("Artists", FileNameSanitizer.SanitizeSegment(searchArtist.Name)),
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
            throw new InvalidOperationException("Playlist search result does not contain enough identifiers.");
        }

        var tracks = playlist.Tracks?
            .Select(container => container.Track)
            .Where(track => track is not null)
            .Cast<YTrack>()
            .ToList() ?? [];

        var owner = playlist.Owner?.Name ?? playlist.Owner?.Login ?? playlist.Owner?.Uid ?? "Playlist";

        return new EntityDetail
        {
            Title = playlist.Title,
            Subtitle = owner,
            Description = playlist.Description ?? $"{tracks.Count} tracks",
            FolderHint = Path.Combine("Playlists", FileNameSanitizer.SanitizeSegment($"{owner} - {playlist.Title}")),
            CanDownloadAll = tracks.Count > 0,
            IsBrowseOnly = false,
            Tracks = tracks.Select(ToTrackEntry).ToList()
        };
    }

    private static EntityDetail BuildVideoDetail(YSearchVideoModel video)
    {
        return new EntityDetail
        {
            Title = video.Title,
            Subtitle = "Browse only",
            Description = string.IsNullOrWhiteSpace(video.YoutubeUrl)
                ? video.Text ?? "The API returns video metadata, but no downloadable audio track mapping."
                : $"{video.Text}\n{video.YoutubeUrl}",
            FolderHint = string.Empty,
            CanDownloadAll = false,
            IsBrowseOnly = true,
            Tracks = []
        };
    }

    private static EntityDetail BuildUserDetail(YSearchUserModel user)
    {
        return new EntityDetail
        {
            Title = "User result",
            Subtitle = "Browse only",
            Description = "The current package exposes user search hits without fields that can be turned into downloadable tracks.",
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
        return string.IsNullOrWhiteSpace(artistLabel) ? "Unknown artist" : artistLabel;
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
            throw new InvalidOperationException("Connect with a Yandex token first.");
        }
    }
}
