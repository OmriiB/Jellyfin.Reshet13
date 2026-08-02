using System.Globalization;
using System.Reflection;
using Jellyfin.Plugin.Reshet13.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Reshet13;

/// <summary>
/// The main Reshet 13 plugin.
/// </summary>
public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private static Plugin? _instance;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">The Jellyfin application paths.</param>
    /// <param name="xmlSerializer">The Jellyfin XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        _instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Reshet 13";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("13c0a101-6f42-4a8e-9d21-5b7c8e4f2a63");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin Instance =>
        _instance ?? throw new InvalidOperationException("The Reshet 13 plugin instance is not available.");

    /// <summary>
    /// Gets a value used by Jellyfin to invalidate cached channel data.
    /// </summary>
    public string DataVersion =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Assembly.GetExecutingAssembly().GetName().Version}-{Configuration.Catalogs.GetHashCode(StringComparison.Ordinal)}-{Configuration.MaximumSeries}-{Configuration.CacheMinutes}");

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        string rootNamespace = typeof(Plugin).Namespace
            ?? throw new InvalidOperationException("The plugin namespace is not available.");

        return
        [
            new PluginPageInfo
            {
                Name = "Reshet13",
                EmbeddedResourcePath = $"{rootNamespace}.Configuration.configPage.html",
            },
            new PluginPageInfo
            {
                Name = "configPage.js",
                EmbeddedResourcePath = $"{rootNamespace}.Configuration.configPage.js",
            },
        ];
    }
}
