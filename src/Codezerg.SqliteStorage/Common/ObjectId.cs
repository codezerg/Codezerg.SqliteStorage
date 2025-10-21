using System;
using System.Security.Cryptography;
using System.Threading;

namespace Codezerg.SqliteStorage.Common;

/// <summary>
/// Provides helpers to generate and work with 12-byte ObjectId-like identifiers.
/// </summary>
/// <remarks>
/// <para>
/// The 12-byte layout is:
/// <list type="table">
///   <listheader><term>Byte range</term><description>Description</description></listheader>
///   <item><term>[0..3]</term><description>Unix timestamp (big-endian, seconds)</description></item>
///   <item><term>[4..8]</term><description>5 cryptographically strong random bytes</description></item>
///   <item><term>[9..11]</term><description>24-bit counter (big-endian)</description></item>
/// </list>
/// </para>
/// <para>
/// Thread-safety: <see cref="Generate"/> is thread-safe. Random bytes are produced under a lock and
/// the 24-bit counter uses <see cref="Interlocked.Increment(ref int)"/>.
/// Other methods are pure and thread-safe for concurrent use.
/// </para>
/// <para>.NET Standard 2.0 compatible.</para>
/// </remarks>
internal static class ObjectId
{
    // 24-bit counter, seeded randomly at startup
    private static int _counter = new Random().Next(0x01000000); // [0, 2^24)
    private static readonly object _rngLock = new object();
    private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();

    /// <summary>
    /// Generates a new 12-byte ObjectId-like value.
    /// </summary>
    /// <returns>The generated 12-byte identifier.</returns>
    /// <remarks>
    /// <para>Layout: timestamp (4 bytes, big-endian), random (5 bytes), counter (3 bytes, big-endian).</para>
    /// <para>Thread-safe.</para>
    /// </remarks>
    /// <example>
    /// <code language="csharp"><![CDATA[
    /// byte[] id = ObjectId.Generate();
    /// string hex = ObjectId.ToHexString(id); // 24 lowercase hex chars
    /// ]]></code>
    /// </example>
    public static byte[] Generate()
    {
        var bytes = new byte[12];

        // 4 bytes: timestamp (big-endian)
        int ts = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        bytes[0] = (byte)(ts >> 24);
        bytes[1] = (byte)(ts >> 16);
        bytes[2] = (byte)(ts >> 8);
        bytes[3] = (byte)ts;

        // 5 bytes: cryptographically strong random
        var randomBytes = new byte[5];
        lock (_rngLock)
        {
            _rng.GetBytes(randomBytes);
        }
        Buffer.BlockCopy(randomBytes, 0, bytes, 4, 5);

        // 3 bytes: 24-bit counter (big-endian)
        int next = Interlocked.Increment(ref _counter) & 0x00FF_FFFF;
        bytes[9] = (byte)(next >> 16);
        bytes[10] = (byte)(next >> 8);
        bytes[11] = (byte)next;

        return bytes;
    }

    /// <summary>
    /// Converts a hex string to a byte array.
    /// </summary>
    /// <param name="hex">Hex string (case-insensitive) with even length.</param>
    /// <returns>The decoded byte array.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hex"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="hex"/> has odd length or contains invalid hex characters.</exception>
    /// <remarks>
    /// This method accepts both uppercase and lowercase characters and requires an even number of characters.
    /// For ObjectId values specifically, the expected length is 24 characters.
    /// </remarks>
    public static byte[] FromHexString(string hex)
    {
        if (hex == null)
            throw new ArgumentNullException(nameof(hex));
        if (hex.Length % 2 != 0)
            throw new ArgumentException("Hex string must have even length.", nameof(hex));

        var result = new byte[hex.Length / 2];
        for (int i = 0; i < result.Length; i++)
        {
            string chunk = hex.Substring(i * 2, 2);
            result[i] = byte.Parse(chunk, System.Globalization.NumberStyles.HexNumber);
        }
        return result;
    }

    /// <summary>
    /// Converts a byte array to a lowercase hex string without separators.
    /// </summary>
    /// <param name="bytes">The input bytes.</param>
    /// <returns>A lowercase hex string.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is <c>null</c>.</exception>
    /// <example>
    /// <code language="csharp"><![CDATA[
    /// var hex = ObjectId.ToHexString(new byte[] { 0xAB, 0xCD }); // "abcd"
    /// ]]></code>
    /// </example>
    public static string ToHexString(byte[] bytes)
    {
        if (bytes == null)
            throw new ArgumentNullException(nameof(bytes));

        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Extracts the UTC creation time from a 12-byte ObjectId-like value.
    /// </summary>
    /// <param name="bytes">The 12-byte identifier.</param>
    /// <returns>
    /// The UTC <see cref="DateTime"/> encoded in the first 4 bytes (Unix seconds, big-endian),
    /// or <see cref="DateTime.MinValue"/> if <paramref name="bytes"/> is <c>null</c> or not 12 bytes long.
    /// </returns>
    /// <remarks>
    /// No validation is performed beyond length; invalid data will yield a <see cref="DateTime"/> that may not be meaningful.
    /// </remarks>
    public static DateTime GetCreationTime(byte[] bytes)
    {
        if (bytes == null || bytes.Length != 12)
            return DateTime.MinValue;

        int timestamp =
            bytes[0] << 24 |
            bytes[1] << 16 |
            bytes[2] << 8 |
            bytes[3];

        return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddSeconds(timestamp);
    }

    /// <summary>
    /// Computes a stable, non-cryptographic hash code for a byte array.
    /// </summary>
    /// <param name="bytes">The input bytes, or <c>null</c>.</param>
    /// <returns>
    /// A deterministic hash code; returns <c>0</c> when <paramref name="bytes"/> is <c>null</c>.
    /// </returns>
    /// <remarks>
    /// Intended for use in collections (e.g., dictionary keys). Do not use for security-sensitive purposes.
    /// </remarks>
    public static int ComputeHashCode(byte[] bytes)
    {
        if (bytes == null)
            return 0;

        unchecked
        {
            int hash = 17;
            foreach (var b in bytes)
            {
                hash = hash * 31 + b;
            }
            return hash;
        }
    }


    /// <summary>
    /// Determines whether two byte array identifiers are equal by value.
    /// </summary>
    /// <param name="x">The first byte array to compare. May be <see langword="null"/>.</param>
    /// <param name="y">The second byte array to compare. May be <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> if both arrays are <see langword="null"/>, reference-equal,
    /// or contain identical bytes in the same order; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This method performs a byte-by-byte comparison and does not allocate memory.
    /// It is intended for use in lightweight identifier types such as <c>DocumentId</c>
    /// or similar <c>*Id</c> structs that wrap an immutable byte sequence.
    /// </remarks>
    public static bool BytesEqual(byte[]? x, byte[]? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        if (x.Length != y.Length) return false;

        for (int i = 0; i < x.Length; i++)
        {
            if (x[i] != y[i]) return false;
        }
        return true;
    }

    /// <summary>
    /// Compares two byte array identifiers using lexicographic order.
    /// </summary>
    /// <param name="x">The first byte array to compare. May be <see langword="null"/>.</param>
    /// <param name="y">The second byte array to compare. May be <see langword="null"/>.</param>
    /// <returns>
    /// A signed integer that indicates the relative order of the arrays:
    /// <list type="bullet">
    /// <item><description>Less than zero — <paramref name="x"/> precedes <paramref name="y"/>.</description></item>
    /// <item><description>Zero — the arrays are equal in content.</description></item>
    /// <item><description>Greater than zero — <paramref name="x"/> follows <paramref name="y"/>.</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// The comparison is performed byte-by-byte in ascending order.  
    /// If all compared bytes are equal, the shorter array is considered smaller.  
    /// <see langword="null"/> values are treated as less than non-<see langword="null"/> values.
    /// </remarks>
    public static int BytesCompare(byte[]? x, byte[]? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int len = Math.Min(x.Length, y.Length);
        for (int i = 0; i < len; i++)
        {
            int cmp = x[i].CompareTo(y[i]);
            if (cmp != 0) return cmp;
        }
        return x.Length.CompareTo(y.Length);
    }
}
