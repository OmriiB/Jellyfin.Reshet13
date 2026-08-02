using Jellyfin.Plugin.Reshet13.Models;
using Jellyfin.Plugin.Reshet13.Services;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Reshet13;

/// <summary>
/// Exposes the Reshet 13 catalog as a Jellyfin channel.
/// </summary>
public sealed class Reshet13Channel :
    IChannel,
    IDisableMediaSourceDisplay,
    IRequiresMediaInfoCallback
{
    private readonly Reshet13CatalogService _catalogService;
    private readonly ILogger<Reshet13Channel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Reshet13Channel"/> class.
    /// </summary>
    /// <param name="catalogService">The cached catalog service.</param>
    /// <param name="logger">The logger.</param>
    public Reshet13Channel(
        Reshet13CatalogService catalogService,
        ILogger<Reshet13Channel> logger)
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "רשת 13";

    /// <inheritdoc />
    public string Description =>
        "Series and episodes streamed from the public Reshet 13 catalog.";

    /// <inheritdoc />
    public string DataVersion => Plugin.Instance.DataVersion;

    /// <inheritdoc />
    public string HomePageUrl => "https://13tv.co.il/";

    /// <inheritdoc />
    public ChannelParentalRating ParentalRating =>
        ChannelParentalRating.GeneralAudience;

    /// <inheritdoc />
    public InternalChannelFeatures GetChannelFeatures()
    {
        return new InternalChannelFeatures
        {
            ContentTypes =
            [
                ChannelMediaContentType.Episode,
            ],
            MediaTypes =
            [
                ChannelMediaType.Video,
            ],
        };
    }

    /// <inheritdoc />
    public Task<DynamicImageResponse> GetChannelImage(
        ImageType type,
        CancellationToken cancellationToken)
    {
        throw new ArgumentException($"Unsupported channel image type: {type}", nameof(type));
    }

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedChannelImages()
    {
        return [];
    }

    /// <inheritdoc />
    public async Task<ChannelItemResult> GetChannelItems(
        InternalChannelItemQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query.FolderId))
            {
                return GetCatalogs();
            }

            Guid folderId = Guid.Parse(query.FolderId);
            var values = Reshet13IdService.FromGuid(folderId);

            if (values.First == Reshet13IdService.CatalogPrefix)
            {
                return await GetSeriesAsync(values.Second, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (values.First == Reshet13IdService.SeriesPrefix)
            {
                return await GetSeasonsAsync(values.Second, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (values.First == Reshet13IdService.SeasonPrefix)
            {
                return await GetEpisodesAsync(
                        values.Second,
                        values.Third,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return EmptyResult();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to retrieve Reshet 13 channel items for folder {FolderId}",
                query.FolderId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<MediaSourceInfo>> GetChannelItemMediaInfo(
        string id,
        CancellationToken cancellationToken)
    {
        // The manifest lives on the episode page, so it is resolved on playback
        // instead of fetching one page per episode while browsing.
        try
        {
            var values = Reshet13IdService.FromGuid(Guid.Parse(id));

            if (values.First != Reshet13IdService.EpisodePrefix)
            {
                return [];
            }

            Reshet13Stream? stream = await _catalogService
                .GetStreamAsync(values.Second, values.Fourth, cancellationToken)
                .ConfigureAwait(false);

            if (stream is null)
            {
                return [];
            }

            return [CreateMediaSource(id, stream)];
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to resolve Reshet 13 stream for {Id}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public bool IsEnabledFor(string userId)
    {
        return Plugin.Instance.Configuration.IsEnabled;
    }

    private static ChannelItemResult GetCatalogs()
    {
        List<ChannelItemInfo> items = Reshet13CatalogService.GetCatalogs()
            .Select(catalog => new ChannelItemInfo
            {
                Id = Reshet13IdService
                    .ToGuid(Reshet13IdService.CatalogPrefix, catalog.Index, 0, 0)
                    .ToString(),
                Name = catalog.Name,
                FolderType = ChannelFolderType.Container,
                Type = ChannelItemType.Folder,
            })
            .ToList();

        return Result(items);
    }

    private async Task<ChannelItemResult> GetSeriesAsync(
        int catalogIndex,
        CancellationToken cancellationToken)
    {
        Reshet13Catalog? catalog = Reshet13CatalogService.GetCatalogs()
            .FirstOrDefault(item => item.Index == catalogIndex);

        if (catalog is null)
        {
            return EmptyResult();
        }

        IReadOnlyList<Reshet13Series> series = await _catalogService
            .GetSeriesAsync(catalog, cancellationToken)
            .ConfigureAwait(false);

        return Result(series.Select(CreateSeriesItem).ToList());
    }

    private async Task<ChannelItemResult> GetSeasonsAsync(
        int seriesId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Reshet13Episode> episodes = await _catalogService
            .GetEpisodesAsync(seriesId, cancellationToken)
            .ConfigureAwait(false);

        List<ChannelItemInfo> items = episodes
            .GroupBy(episode => episode.SeasonNumber)
            .OrderBy(group => group.Key)
            .Select(group => CreateSeasonItem(seriesId, group.Key, group.First()))
            .ToList();

        return Result(items);
    }

    private async Task<ChannelItemResult> GetEpisodesAsync(
        int seriesId,
        int seasonNumber,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Reshet13Episode> episodes = await _catalogService
            .GetEpisodesAsync(seriesId, cancellationToken)
            .ConfigureAwait(false);

        List<ChannelItemInfo> items = episodes
            .Where(episode => episode.SeasonNumber == seasonNumber)
            .Select(episode => CreateEpisodeItem(seriesId, episode))
            .ToList();

        return Result(items);
    }

    private static ChannelItemInfo CreateSeriesItem(Reshet13Series series)
    {
        return new ChannelItemInfo
        {
            Id = Reshet13IdService
                .ToGuid(Reshet13IdService.SeriesPrefix, series.Id, 0, 0)
                .ToString(),
            Name = series.Title,
            SeriesName = series.Title,
            Overview = series.Overview,
            ImageUrl = series.Images.Primary
                ?? series.Images.Backdrop
                ?? series.Images.Thumb,
            // Container, not Series: a Series folder makes Jellyfin run its metadata
            // providers, which rename Hebrew titles to unrelated TMDb matches.
            FolderType = ChannelFolderType.Container,
            Type = ChannelItemType.Folder,
        };
    }

    private static ChannelItemInfo CreateSeasonItem(
        int seriesId,
        int seasonNumber,
        Reshet13Episode firstEpisode)
    {
        return new ChannelItemInfo
        {
            Id = Reshet13IdService
                .ToGuid(Reshet13IdService.SeasonPrefix, seriesId, seasonNumber, 0)
                .ToString(),
            Name = $"עונה {seasonNumber}",
            SeriesName = firstEpisode.SeriesTitle,
            IndexNumber = seasonNumber,
            ImageUrl = firstEpisode.Images.Primary
                ?? firstEpisode.Images.Backdrop
                ?? firstEpisode.Images.Thumb,
            FolderType = ChannelFolderType.Container,
            Type = ChannelItemType.Folder,
        };
    }

    private static ChannelItemInfo CreateEpisodeItem(
        int seriesId,
        Reshet13Episode episode)
    {
        return new ChannelItemInfo
        {
            Id = Reshet13IdService
                .ToGuid(
                    Reshet13IdService.EpisodePrefix,
                    seriesId,
                    episode.SeasonNumber,
                    episode.Id)
                .ToString(),
            Name = episode.Title,
            SeriesName = episode.SeriesTitle,
            Overview = episode.Overview,
            IndexNumber = episode.EpisodeNumber > 0
                ? episode.EpisodeNumber
                : null,
            ParentIndexNumber = episode.SeasonNumber,
            RunTimeTicks = episode.RunTimeTicks,
            ImageUrl = episode.Images.Backdrop
                ?? episode.Images.Thumb
                ?? episode.Images.Primary,
            ContentType = ChannelMediaContentType.Episode,
            MediaType = ChannelMediaType.Video,
            Type = ChannelItemType.Media,
            IsLiveStream = false,
        };
    }

    private static MediaSourceInfo CreateMediaSource(string id, Reshet13Stream stream)
    {
        Dictionary<string, string> requiredHeaders = [];
        string userAgent = Plugin.Instance.Configuration.UserAgent;

        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            requiredHeaders["User-Agent"] = userAgent;
        }

        return new MediaSourceInfo
        {
            Id = id,
            Name = "Reshet 13",
            Path = stream.Url,
            Protocol = MediaProtocol.Http,
            EncoderProtocol = MediaProtocol.Http,
            Container = stream.Container,
            IsRemote = true,
            IsInfiniteStream = false,
            SupportsDirectPlay = true,
            SupportsDirectStream = true,
            SupportsProbing = true,
            RequiresOpening = false,
            RequiresClosing = false,
            RequiredHttpHeaders = requiredHeaders,
        };
    }

    private static ChannelItemResult Result(List<ChannelItemInfo> items)
    {
        return new ChannelItemResult
        {
            Items = items,
            TotalRecordCount = items.Count,
        };
    }

    private static ChannelItemResult EmptyResult()
    {
        return new ChannelItemResult
        {
            Items = [],
            TotalRecordCount = 0,
        };
    }
}
