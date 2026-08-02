using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Reshet13.Configuration;

/// <summary>
/// The plugin configuration.
/// </summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the channel is visible.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the catalog pages, one <c>Name|URL</c> pair per line.
    /// Each becomes a top level folder in the channel.
    /// </summary>
    public string Catalogs { get; set; } = string.Join(
        '\n',
        "כל התוכניות|https://13tv.co.il/allshows/",
        "חדשות 13|https://13tv.co.il/news/");

    /// <summary>
    /// Gets or sets the maximum number of series taken from each catalog.
    /// </summary>
    public int MaximumSeries { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the HTTP user agent used for Reshet 13 requests.
    /// </summary>
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

    /// <summary>
    /// Gets or sets the number of minutes catalog pages remain cached.
    /// </summary>
    /// <remarks>
    /// A catalog is one page read and a series is one more, so this is far cheaper
    /// than a scraped site and can be refreshed several times a day.
    /// </remarks>
    public int CacheMinutes { get; set; } = 360;
}
