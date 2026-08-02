# Jellyfin Reshet 13

A Jellyfin 10.11.x channel plugin that exposes the public
[Reshet 13](https://13tv.co.il/) catalog as browsable series, seasons and
episodes with Hebrew metadata, artwork and direct HLS playback.

## Installation

Add the repository to **Dashboard → Plugins → Repositories**:

```
https://raw.githubusercontent.com/OmriiB/Jellyfin.Reshet13/main/manifest.json
```

Then install **Reshet 13** from the catalog and restart Jellyfin. The channel
appears under **Channels** as `רשת 13`.

## Configuration

| Setting | Default | Meaning |
| --- | --- | --- |
| Catalog pages | two lines, see below | One `Name\|URL` pair per line; each becomes a folder |
| Maximum series | 1000 | Upper bound per catalog |
| Cache duration | 360 minutes | How long a catalog page stays cached |
| HTTP user agent | a desktop Chrome string | Sent with every request |

The default catalogs are:

```
כל התוכניות|https://13tv.co.il/allshows/
חדשות 13|https://13tv.co.il/news/
```

## Data source

13tv.co.il is a Next.js front end over Kaltura OTT. Every page ships the data
its React tree was rendered from inside a `__NEXT_DATA__` script tag, so the
plugin reads structured JSON rather than scraping markup:

```
props.pageProps.tree    categories
props.pageProps.leafs   rails, each holding catalog assets
```

Each asset carries the fields the channel needs directly:

| Field | Used for |
| --- | --- |
| `name` | Series and episode title |
| `metas.SeasonNumber` | Season grouping |
| `metas.EpisodeNumber` | Episode index |
| `metas.RunTime` | Runtime, in seconds |
| `metas.LongSummary` / `ShortSummary` | Overview |
| `images[].ratio` | `9x16` becomes the poster, `16x9` the backdrop |

The one value still taken from markup is the playback manifest, which the
player writes into the episode page:

```
https://reshet.g-mana.live/media/<uuid>/mainManifest.m3u8
```

The manifest is served without DRM, without an entitlement token and without a
session, so Jellyfin plays it directly.

### Why the plugin does not pretend to be a browser

The site runs bot detection that checks whether a request is internally
consistent, and a user agent naming a browser is held to that browser's
behaviour. Measured against `13tv.co.il/allshows/` from the same host:

| User agent | HTTP version | Response |
| --- | --- | --- |
| Chrome | 1.1 | 403 |
| Chrome | 2 | 200 |
| `Jellyfin-Reshet13/1.0` | 1.1 | 200 |
| none | 1.1 | 200 |

A Chrome user agent over HTTP/1.1 is rejected because a real Chrome would have
negotiated HTTP/2. `HttpClient` sends HTTP/1.1 by default, so a plugin copying a
browser user agent is blocked while an honest one is served normally.

The plugin therefore identifies itself truthfully and additionally requests
HTTP/2, which keeps an older configuration that still holds a browser user agent
working.

### Notes on robustness

Because the page ships its own data, this plugin does not depend on CSS classes
or element order the way a scraped site would. Two things are still inferred
defensively:

- **The page path of an asset.** The field holding it differs per rail
  template, so every string in the asset is checked for a site path instead of
  trusting one field name.
- **The season of an episode.** Shows that never declare `SeasonNumber` still
  carry it in the episode path as `/season-NN/`.

## Geo restriction

Reshet 13 assets carry a `geoBlockRuleId` and the streams are restricted to
Israel. The Jellyfin server must reach the site from an Israeli IP address, and
the restriction applies to the server rather than to the client that browses it.

## Licence and content

The plugin reads pages that are public and free to view, and plays a manifest
the site serves unauthenticated. It does not log in, does not decrypt anything
and does not circumvent any access control. All content remains the property of
Reshet 13.

## Building

The project targets .NET 9. CI builds it on every push; to build locally:

```
dotnet build Jellyfin.Plugin.Reshet13/Jellyfin.Plugin.Reshet13.csproj -c Release
```

Releasing is driven by a tag:

```
git tag v0.1.0.0
git push origin v0.1.0.0
```

The release workflow builds the DLL, publishes a GitHub release and regenerates
`manifest.json` on `main`.
