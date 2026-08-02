namespace Jellyfin.Plugin.Reshet13.Models;

/// <summary>
/// A top level catalog page that becomes a folder in the channel.
/// </summary>
/// <param name="Index">The position of the catalog in the configuration.</param>
/// <param name="Name">The folder name shown in Jellyfin.</param>
/// <param name="Url">The absolute page URL that lists the series.</param>
public sealed record Reshet13Catalog(int Index, string Name, string Url);

/// <summary>
/// The artwork of a catalog item.
/// </summary>
/// <param name="Primary">The portrait poster, when the site publishes one.</param>
/// <param name="Backdrop">The landscape image.</param>
/// <param name="Thumb">Any remaining image.</param>
public sealed record Reshet13Images(string? Primary, string? Backdrop, string? Thumb);

/// <summary>
/// A series listed by a catalog page.
/// </summary>
/// <param name="Id">The stable identifier derived from the series page path.</param>
/// <param name="Title">The series title.</param>
/// <param name="Overview">The series synopsis, when published.</param>
/// <param name="Url">The absolute series page URL.</param>
/// <param name="Images">The series artwork.</param>
public sealed record Reshet13Series(
    int Id,
    string Title,
    string? Overview,
    string Url,
    Reshet13Images Images);

/// <summary>
/// An episode of a series.
/// </summary>
/// <param name="Id">The stable identifier derived from the episode page path.</param>
/// <param name="SeriesTitle">The title of the owning series.</param>
/// <param name="Title">The episode title.</param>
/// <param name="Overview">The episode synopsis, when published.</param>
/// <param name="SeasonNumber">The season number, defaulting to 1.</param>
/// <param name="EpisodeNumber">The episode number, or 0 when unknown.</param>
/// <param name="RunTimeTicks">The runtime in ticks, when published.</param>
/// <param name="Url">The absolute episode page URL.</param>
/// <param name="Images">The episode artwork.</param>
public sealed record Reshet13Episode(
    int Id,
    string SeriesTitle,
    string Title,
    string? Overview,
    int SeasonNumber,
    int EpisodeNumber,
    long? RunTimeTicks,
    string Url,
    Reshet13Images Images);

/// <summary>
/// A playable stream.
/// </summary>
/// <param name="Url">The absolute manifest URL.</param>
/// <param name="Container">The container reported to Jellyfin.</param>
public sealed record Reshet13Stream(string Url, string Container);
