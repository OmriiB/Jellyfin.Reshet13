using Jellyfin.Plugin.Reshet13.Client;
using Jellyfin.Plugin.Reshet13.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Jellyfin.Plugin.Reshet13.Services;

/// <summary>
/// Provides cached access to the configured Reshet 13 catalogs.
/// </summary>
public sealed class Reshet13CatalogService
{
    private readonly IReshet13ApiClient _apiClient;
    private readonly IMemoryCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="Reshet13CatalogService"/> class.
    /// </summary>
    /// <param name="apiClient">The catalog client.</param>
    /// <param name="cache">The memory cache.</param>
    public Reshet13CatalogService(IReshet13ApiClient apiClient, IMemoryCache cache)
    {
        _apiClient = apiClient;
        _cache = cache;
    }

    /// <summary>
    /// Gets the configured catalogs.
    /// </summary>
    /// <returns>The catalogs in configuration order.</returns>
    public static IReadOnlyList<Reshet13Catalog> GetCatalogs()
    {
        List<Reshet13Catalog> catalogs = [];

        string[] lines = Plugin.Instance.Configuration.Catalogs
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries);

        foreach (string line in lines)
        {
            int separator = line.IndexOf('|', StringComparison.Ordinal);

            string url = separator >= 0 ? line[(separator + 1)..].Trim() : line;
            string name = separator > 0 ? line[..separator].Trim() : url;

            if (Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                catalogs.Add(new Reshet13Catalog(catalogs.Count, name, url));
            }
        }

        return catalogs;
    }

    /// <summary>
    /// Gets the cached series of a single catalog.
    /// </summary>
    /// <param name="catalog">The catalog to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The series.</returns>
    public async Task<IReadOnlyList<Reshet13Series>> GetSeriesAsync(
        Reshet13Catalog catalog,
        CancellationToken cancellationToken)
    {
        string key = $"reshet13:series:{catalog.Url}";

        if (_cache.TryGetValue(key, out IReadOnlyList<Reshet13Series>? cached) && cached is not null)
        {
            return cached;
        }

        IReadOnlyList<Reshet13Series> series = await _apiClient
            .GetSeriesAsync(catalog, cancellationToken)
            .ConfigureAwait(false);

        _cache.Set(key, series, TimeSpan.FromMinutes(GetCacheMinutes()));

        return series;
    }

    /// <summary>
    /// Gets cached episodes for a series.
    /// </summary>
    /// <param name="seriesId">The series identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The episodes, empty when the series is no longer listed.</returns>
    public async Task<IReadOnlyList<Reshet13Episode>> GetEpisodesAsync(
        int seriesId,
        CancellationToken cancellationToken)
    {
        string key = $"reshet13:episodes:{seriesId}";

        if (_cache.TryGetValue(key, out IReadOnlyList<Reshet13Episode>? cached) && cached is not null)
        {
            return cached;
        }

        Reshet13Series? series = await FindSeriesAsync(seriesId, cancellationToken)
            .ConfigureAwait(false);

        if (series is null)
        {
            return [];
        }

        IReadOnlyList<Reshet13Episode> episodes = await _apiClient
            .GetEpisodesAsync(series, cancellationToken)
            .ConfigureAwait(false);

        _cache.Set(key, episodes, TimeSpan.FromMinutes(GetCacheMinutes()));

        return episodes;
    }

    /// <summary>
    /// Resolves the playable stream for an episode.
    /// </summary>
    /// <param name="seriesId">The series identifier.</param>
    /// <param name="episodeId">The episode identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stream, or <c>null</c> when it cannot be resolved.</returns>
    public async Task<Reshet13Stream?> GetStreamAsync(
        int seriesId,
        int episodeId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Reshet13Episode> episodes = await GetEpisodesAsync(seriesId, cancellationToken)
            .ConfigureAwait(false);

        Reshet13Episode? episode = episodes.FirstOrDefault(item => item.Id == episodeId);
        if (episode is null)
        {
            return null;
        }

        string key = $"reshet13:stream:{episodeId}";

        if (_cache.TryGetValue(key, out Reshet13Stream? cached) && cached is not null)
        {
            return cached;
        }

        Reshet13Stream? stream = await _apiClient
            .GetStreamAsync(episode.Url, cancellationToken)
            .ConfigureAwait(false);

        if (stream is not null)
        {
            // A manifest URL is signed for a limited window on some shows, so it is
            // cached for minutes rather than for the catalog lifetime.
            _cache.Set(key, stream, TimeSpan.FromMinutes(10));
        }

        return stream;
    }

    private async Task<Reshet13Series?> FindSeriesAsync(
        int seriesId,
        CancellationToken cancellationToken)
    {
        foreach (Reshet13Catalog catalog in GetCatalogs())
        {
            IReadOnlyList<Reshet13Series> series = await GetSeriesAsync(catalog, cancellationToken)
                .ConfigureAwait(false);

            Reshet13Series? match = series.FirstOrDefault(item => item.Id == seriesId);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static int GetCacheMinutes()
    {
        return Math.Clamp(Plugin.Instance.Configuration.CacheMinutes, 1, 10080);
    }
}
