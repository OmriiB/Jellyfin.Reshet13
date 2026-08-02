using System.Net;
using Jellyfin.Plugin.Reshet13.Client;
using Jellyfin.Plugin.Reshet13.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Reshet13;

/// <summary>
/// Registers the plugin services.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(
        IServiceCollection serviceCollection,
        IServerApplicationHost applicationHost)
    {
        serviceCollection.AddMemoryCache();

        // The site serves compressed responses and returns an empty body to a
        // client that cannot accept them.
        serviceCollection.AddHttpClient<IReshet13ApiClient, Reshet13ApiClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
            });
        serviceCollection.AddSingleton<Reshet13CatalogService>();
        serviceCollection.AddSingleton<IChannel, Reshet13Channel>();
    }
}
