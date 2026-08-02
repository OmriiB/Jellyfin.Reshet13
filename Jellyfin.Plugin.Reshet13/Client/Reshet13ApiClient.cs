using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.Reshet13.Models;
using Jellyfin.Plugin.Reshet13.Services;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Reshet13.Client;

/// <summary>
/// Reads the Reshet 13 catalog from the JSON the site embeds in every page.
/// </summary>
/// <remarks>
/// 13tv.co.il is a Next.js front end over Kaltura OTT. Every page ships the data
/// its React tree was rendered from inside a <c>__NEXT_DATA__</c> script tag, so
/// the catalog is read as structured JSON rather than scraped out of markup. The
/// only value still taken from the markup is the playback manifest, which the
/// player writes into the episode page.
/// </remarks>
public sealed partial class Reshet13ApiClient : IReshet13ApiClient
{
    private const string SiteRoot = "https://13tv.co.il";

    private readonly HttpClient _httpClient;
    private readonly ILogger<Reshet13ApiClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Reshet13ApiClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="logger">The logger.</param>
    public Reshet13ApiClient(HttpClient httpClient, ILogger<Reshet13ApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Reshet13Series>> GetSeriesAsync(
        Reshet13Catalog catalog,
        CancellationToken cancellationToken)
    {
        string? html = await GetPageAsync(catalog.Url, cancellationToken)
            .ConfigureAwait(false);

        if (html is null)
        {
            return [];
        }

        List<Reshet13Series> series = [];
        HashSet<int> seen = [];

        foreach (JsonElement asset in EnumerateAssets(html))
        {
            // A catalog page mixes series cards with the articles and clips that
            // promote them, and only the series carry a browsable page of their own.
            if (!IsSeriesAsset(asset))
            {
                continue;
            }

            string? url = FindPagePath(asset, catalog.Url);
            string? title = GetText(asset, "name");

            if (url is null || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            int id = Reshet13IdService.StableTextId(new Uri(url).AbsolutePath);
            if (!seen.Add(id))
            {
                continue;
            }

            series.Add(new Reshet13Series(
                id,
                title.Trim(),
                GetOverview(asset),
                url,
                GetImages(asset)));

            if (series.Count >= GetMaximumSeries())
            {
                break;
            }
        }

        _logger.LogInformation(
            "Read {Count} Reshet 13 series from {Url}",
            series.Count,
            catalog.Url);

        return series;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Reshet13Episode>> GetEpisodesAsync(
        Reshet13Series series,
        CancellationToken cancellationToken)
    {
        string? html = await GetPageAsync(series.Url, cancellationToken)
            .ConfigureAwait(false);

        if (html is null)
        {
            return [];
        }

        List<Reshet13Episode> episodes = [];
        HashSet<int> seen = [];

        foreach (JsonElement asset in EnumerateAssets(html))
        {
            if (!IsEpisodeAsset(asset))
            {
                continue;
            }

            string? url = FindPagePath(asset, series.Url);
            string? title = GetText(asset, "name");

            if (url is null || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            // Episodes of other shows appear in the "you may also like" rails, and
            // every episode of this show lives under the series path.
            if (!IsWithinSeries(url, series.Url))
            {
                continue;
            }

            int id = Reshet13IdService.StableTextId(new Uri(url).AbsolutePath);
            if (!seen.Add(id))
            {
                continue;
            }

            episodes.Add(new Reshet13Episode(
                id,
                series.Title,
                title.Trim(),
                GetOverview(asset),
                GetSeasonNumber(asset, url),
                GetNumber(asset, "EpisodeNumber") ?? 0,
                GetRunTimeTicks(asset),
                url,
                GetImages(asset)));
        }

        return episodes
            .OrderBy(episode => episode.SeasonNumber)
            .ThenBy(episode => episode.EpisodeNumber)
            .ThenBy(episode => episode.Title, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<Reshet13Stream?> GetStreamAsync(
        string episodeUrl,
        CancellationToken cancellationToken)
    {
        string? html = await GetPageAsync(episodeUrl, cancellationToken)
            .ConfigureAwait(false);

        if (html is null)
        {
            return null;
        }

        Match match = ManifestRegex().Match(html);
        if (!match.Success)
        {
            _logger.LogWarning("No Reshet 13 manifest found on {Url}", episodeUrl);
            return null;
        }

        // The JSON the manifest is embedded in escapes every forward slash.
        string url = match.Value.Replace("\\/", "/", StringComparison.Ordinal);

        return new Reshet13Stream(url, "hls");
    }

    [GeneratedRegex(
        @"https?://[^""'\s\\]+?mainManifest\.m3u8",
        RegexOptions.IgnoreCase)]
    private static partial Regex ManifestRegex();

    [GeneratedRegex(
        @"<script[^>]*id=""__NEXT_DATA__""[^>]*>(?<json>.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex NextDataRegex();

    [GeneratedRegex(@"/season-(?<season>\d+)/", RegexOptions.IgnoreCase)]
    private static partial Regex SeasonPathRegex();

    private async Task<string?> GetPageAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, url);

            string userAgent = Plugin.Instance.Configuration.UserAgent;
            if (!string.IsNullOrWhiteSpace(userAgent))
            {
                request.Headers.UserAgent.ParseAdd(userAgent);
            }

            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml");
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("he-IL"));

            using HttpResponseMessage response = await _httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            return await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(exception, "Failed to read {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Yields every catalog asset embedded in a page.
    /// </summary>
    /// <remarks>
    /// The site groups assets into rails under <c>props.pageProps.leafs</c>, but the
    /// exact nesting differs per page template, so the document is walked for any
    /// object that carries the fields an asset is identified by instead.
    /// </remarks>
    private static IEnumerable<JsonElement> EnumerateAssets(string html)
    {
        Match match = NextDataRegex().Match(html);
        if (!match.Success)
        {
            yield break;
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(match.Groups["json"].Value);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (document)
        {
            foreach (JsonElement asset in Walk(document.RootElement, 0))
            {
                yield return asset.Clone();
            }
        }
    }

    private static IEnumerable<JsonElement> Walk(JsonElement element, int depth)
    {
        // The embedded document nests a dozen levels at most, and the bound keeps a
        // malformed or self referencing payload from spinning.
        if (depth > 24)
        {
            yield break;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("name", out _)
                    && (element.TryGetProperty("entryId", out _)
                        || element.TryGetProperty("typeDescription", out _)))
                {
                    yield return element;
                }

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    foreach (JsonElement child in Walk(property.Value, depth + 1))
                    {
                        yield return child;
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    foreach (JsonElement child in Walk(item, depth + 1))
                    {
                        yield return child;
                    }
                }

                break;
        }
    }

    private static bool IsSeriesAsset(JsonElement asset)
    {
        string? type = GetText(asset, "typeDescription");

        if (string.Equals(type, "Series", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Some rails describe a show without a type, and a show is the only asset
        // that carries a series identifier while carrying no episode number.
        return GetNumber(asset, "SeriesID") is not null
            && GetNumber(asset, "EpisodeNumber") is null;
    }

    private static bool IsEpisodeAsset(JsonElement asset)
    {
        string? type = GetText(asset, "typeDescription");

        if (string.Equals(type, "Episode", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return GetNumber(asset, "EpisodeNumber") is not null;
    }

    /// <summary>
    /// Finds the site path an asset links to.
    /// </summary>
    /// <remarks>
    /// The field holding the path differs per rail template, so every string in the
    /// asset is checked for a site path instead of trusting one field name.
    /// </remarks>
    private static string? FindPagePath(JsonElement asset, string pageUrl)
    {
        foreach (string candidate in Strings(asset, 0))
        {
            string value = candidate.Replace("\\/", "/", StringComparison.Ordinal).Trim();

            if (value.StartsWith(SiteRoot, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            if (value.Length > 1
                && value[0] == '/'
                && value[1] != '/'
                && !HasFileExtension(value))
            {
                return SiteRoot + value;
            }
        }

        return null;
    }

    private static IEnumerable<string> Strings(JsonElement element, int depth)
    {
        if (depth > 8)
        {
            yield break;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                string? value = element.GetString();
                if (!string.IsNullOrEmpty(value))
                {
                    yield return value;
                }

                break;

            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    // Images are absolute URLs on a different host and never a page.
                    if (string.Equals(property.Name, "images", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    foreach (string child in Strings(property.Value, depth + 1))
                    {
                        yield return child;
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    foreach (string child in Strings(item, depth + 1))
                    {
                        yield return child;
                    }
                }

                break;
        }
    }

    private static bool HasFileExtension(string path)
    {
        int slash = path.LastIndexOf('/');
        int dot = path.LastIndexOf('.');

        return dot > slash && dot < path.Length - 1;
    }

    private static bool IsWithinSeries(string episodeUrl, string seriesUrl)
    {
        string series = new Uri(seriesUrl).AbsolutePath.Trim('/');
        string episode = new Uri(episodeUrl).AbsolutePath.Trim('/');

        if (series.Length == 0)
        {
            return true;
        }

        // A series lives at /shows/<slug>/ while its episodes live at
        // /item/shows/<slug>/season-NN/episodes/<slug>-<id>/, so the shared part is
        // the show slug rather than a leading path.
        string slug = series[(series.LastIndexOf('/') + 1)..];

        return slug.Length > 0
            && episode.Contains('/' + slug + '/', StringComparison.OrdinalIgnoreCase);
    }

    private static Reshet13Images GetImages(JsonElement asset)
    {
        string? portrait = null;
        string? landscape = null;
        string? other = null;

        if (asset.TryGetProperty("images", out JsonElement images)
            && images.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement image in images.EnumerateArray())
            {
                string? url = GetText(image, "url");
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                url = url.Replace("\\/", "/", StringComparison.Ordinal);
                string ratio = GetText(image, "ratio") ?? string.Empty;

                if (ratio.Equals("9x16", StringComparison.OrdinalIgnoreCase))
                {
                    portrait ??= url;
                }
                else if (ratio.Equals("16x9", StringComparison.OrdinalIgnoreCase))
                {
                    landscape ??= url;
                }
                else
                {
                    other ??= url;
                }
            }
        }

        // Jellyfin renders the primary image as a poster, so the portrait art is
        // preferred and the landscape art is kept as the backdrop.
        return new Reshet13Images(portrait ?? landscape ?? other, landscape, other);
    }

    private static string? GetOverview(JsonElement asset)
    {
        string? overview = GetMetaText(asset, "LongSummary")
            ?? GetMetaText(asset, "ShortSummary")
            ?? GetText(asset, "description");

        return string.IsNullOrWhiteSpace(overview) ? null : overview.Trim();
    }

    private static int GetSeasonNumber(JsonElement asset, string url)
    {
        int? season = GetNumber(asset, "SeasonNumber");
        if (season is > 0)
        {
            return season.Value;
        }

        // Shows that never declare a season still carry it in the episode path.
        Match match = SeasonPathRegex().Match(url);

        return match.Success
            && int.TryParse(
                match.Groups["season"].Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int parsed)
            && parsed > 0
                ? parsed
                : 1;
    }

    private static long? GetRunTimeTicks(JsonElement asset)
    {
        int? runtime = GetNumber(asset, "RunTime");

        // Kaltura reports the runtime in seconds.
        return runtime is > 0
            ? TimeSpan.FromSeconds(runtime.Value).Ticks
            : null;
    }

    /// <summary>
    /// Reads a metadata value, which the site writes either as a bare value or as an
    /// object wrapping it.
    /// </summary>
    private static JsonElement? GetMeta(JsonElement asset, string name)
    {
        if (!asset.TryGetProperty("metas", out JsonElement metas)
            || metas.ValueKind != JsonValueKind.Object
            || !metas.TryGetProperty(name, out JsonElement meta))
        {
            return null;
        }

        if (meta.ValueKind == JsonValueKind.Object
            && meta.TryGetProperty("value", out JsonElement wrapped))
        {
            return wrapped;
        }

        return meta;
    }

    private static string? GetMetaText(JsonElement asset, string name)
    {
        JsonElement? meta = GetMeta(asset, name);

        return meta?.ValueKind == JsonValueKind.String ? meta.Value.GetString() : null;
    }

    private static int? GetNumber(JsonElement asset, string name)
    {
        JsonElement? meta = GetMeta(asset, name);
        if (meta is null)
        {
            return null;
        }

        return meta.Value.ValueKind switch
        {
            JsonValueKind.Number when meta.Value.TryGetInt32(out int number) => number,
            JsonValueKind.String when int.TryParse(
                meta.Value.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int parsed) => parsed,
            _ => null,
        };
    }

    private static string? GetText(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }

    private static int GetMaximumSeries()
    {
        return Math.Clamp(Plugin.Instance.Configuration.MaximumSeries, 1, 5000);
    }
}
