using System.Buffers.Binary;

namespace Jellyfin.Plugin.Reshet13.Services;

/// <summary>
/// Creates stable Jellyfin channel identifiers from Reshet 13 identifiers.
/// </summary>
public static class Reshet13IdService
{
    /// <summary>
    /// The prefix used for catalog folders.
    /// </summary>
    public const int CatalogPrefix = unchecked((int)0x13C0A101);

    /// <summary>
    /// The prefix used for series folders.
    /// </summary>
    public const int SeriesPrefix = unchecked((int)0x13C0A102);

    /// <summary>
    /// The prefix used for season folders.
    /// </summary>
    public const int SeasonPrefix = unchecked((int)0x13C0A103);

    /// <summary>
    /// The prefix used for episode items.
    /// </summary>
    public const int EpisodePrefix = unchecked((int)0x13C0A104);

    /// <summary>
    /// Creates a stable GUID from four integer values.
    /// </summary>
    /// <param name="first">The first value.</param>
    /// <param name="second">The second value.</param>
    /// <param name="third">The third value.</param>
    /// <param name="fourth">The fourth value.</param>
    /// <returns>The stable GUID.</returns>
    public static Guid ToGuid(int first, int second, int third, int fourth)
    {
        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteInt32BigEndian(bytes[0..4], first);
        BinaryPrimitives.WriteInt32BigEndian(bytes[4..8], second);
        BinaryPrimitives.WriteInt32BigEndian(bytes[8..12], third);
        BinaryPrimitives.WriteInt32BigEndian(bytes[12..16], fourth);
        return new Guid(bytes);
    }

    /// <summary>
    /// Decodes four integer values from a stable GUID.
    /// </summary>
    /// <param name="id">The GUID.</param>
    /// <returns>The decoded values.</returns>
    public static (int First, int Second, int Third, int Fourth) FromGuid(Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes);

        return (
            BinaryPrimitives.ReadInt32BigEndian(bytes[0..4]),
            BinaryPrimitives.ReadInt32BigEndian(bytes[4..8]),
            BinaryPrimitives.ReadInt32BigEndian(bytes[8..12]),
            BinaryPrimitives.ReadInt32BigEndian(bytes[12..16]));
    }

    /// <summary>
    /// Creates a deterministic positive integer from a text value.
    /// </summary>
    /// <param name="value">The source value.</param>
    /// <returns>A deterministic positive integer.</returns>
    public static int StableTextId(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char character in value)
            {
                hash ^= character;
                hash *= 16777619;
            }

            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
