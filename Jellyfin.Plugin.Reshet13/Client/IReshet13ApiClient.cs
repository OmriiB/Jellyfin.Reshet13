using Jellyfin.Plugin.Reshet13.Models;

namespace Jellyfin.Plugin.Reshet13.Client;

/// <summary>
/// Reads the public Reshet 13 catalog.
/// </summary>
public interface IReshet13ApiClient
{
    /// <summary>
    /// Reads every series listed by a catalog page.
    /// </summary>
    /// <param name="catalog">The catalog page.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The series in page order.</returns>
    Task<IReadOnlyList<Reshet13Series>> GetSeriesAsync(
        Reshet13Catalog catalog,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads every episode of a series.
    /// </summary>
    /// <param name="series">The series to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The episodes ordered by season and episode number.</returns>
    Task<IReadOnlyList<Reshet13Episode>> GetEpisodesAsync(
        Reshet13Series series,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the playable stream of an episode page.
    /// </summary>
    /// <param name="episodeUrl">The absolute episode page URL.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stream, or <c>null</c> when the page publishes none.</returns>
    Task<Reshet13Stream?> GetStreamAsync(
        string episodeUrl,
        CancellationToken cancellationToken);
}
