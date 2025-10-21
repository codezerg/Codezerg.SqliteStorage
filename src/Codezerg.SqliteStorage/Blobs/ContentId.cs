using Codezerg.SqliteStorage.Common;
using System;
using System.Linq;

namespace Codezerg.SqliteStorage.Blobs;

/// <summary>
/// Represents a unique content identifier
/// </summary>
public readonly struct ContentId : IEquatable<ContentId>, IComparable<ContentId>
{
    private readonly byte[] _value;

    /// <summary>
    /// Gets an empty ContentId.
    /// </summary>
    public static ContentId Empty => new(new byte[12]);

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentId"/> struct.
    /// </summary>
    public ContentId()
    {
        _value = ObjectId.Generate();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentId"/> struct from a byte array.
    /// </summary>
    /// <param name="value">The byte array representing the ID.</param>
    public ContentId(byte[] value)
    {
        if (value == null || value.Length != 12)
            throw new ArgumentException("ContentId must be exactly 12 bytes", nameof(value));

        _value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentId"/> struct from a hex string.
    /// </summary>
    /// <param name="hexString">The hex string representing the ID.</param>
    public ContentId(string hexString)
    {
        if (string.IsNullOrEmpty(hexString) || hexString.Length != 24)
            throw new ArgumentException("ContentId hex string must be exactly 24 characters", nameof(hexString));

        _value = ObjectId.FromHexString(hexString);
    }

    /// <summary>
    /// Gets the timestamp component of this ContentId.
    /// </summary>
    public DateTime Timestamp => ObjectId.GetCreationTime(_value);

    /// <summary>
    /// Generates a new ContentId.
    /// </summary>
    /// <returns>A new unique ContentId.</returns>
    public static ContentId NewId() => new();

    /// <summary>
    /// Parses a hex string into a ContentId.
    /// </summary>
    public static ContentId Parse(string hexString) => new(hexString);

    /// <summary>
    /// Tries to parse a hex string into a ContentId.
    /// </summary>
    public static bool TryParse(string? hexString, out ContentId id)
    {
        if (string.IsNullOrEmpty(hexString) || hexString!.Length != 24)
        {
            id = Empty;
            return false;
        }

        try
        {
            id = new ContentId(hexString);
            return true;
        }
        catch
        {
            id = Empty;
            return false;
        }
    }

    /// <summary>
    /// Converts the ContentId to a hex string.
    /// </summary>
    public override string ToString() => _value == null ? string.Empty : ObjectId.ToHexString(_value);

    /// <summary>
    /// Gets the byte array representation of this ContentId.
    /// </summary>
    public byte[] ToByteArray() => _value ?? Array.Empty<byte>();

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ContentId id && Equals(id);

    /// <inheritdoc/>
    public bool Equals(ContentId other) => ObjectId.BytesEqual(_value, other._value);

    /// <inheritdoc/>
    public override int GetHashCode() => ObjectId.ComputeHashCode(_value);

    /// <inheritdoc/>
    public int CompareTo(ContentId other) => ObjectId.BytesCompare(_value, other._value);

    /// <summary>
    /// Determines whether two ContentId instances are equal.
    /// </summary>
    public static bool operator ==(ContentId left, ContentId right) => left.Equals(right);

    /// <summary>
    /// Determines whether two ContentId instances are not equal.
    /// </summary>
    public static bool operator !=(ContentId left, ContentId right) => !left.Equals(right);

    /// <summary>
    /// Determines whether one ContentId is less than another.
    /// </summary>
    public static bool operator <(ContentId left, ContentId right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Determines whether one ContentId is greater than another.
    /// </summary>
    public static bool operator >(ContentId left, ContentId right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Determines whether one ContentId is less than or equal to another.
    /// </summary>
    public static bool operator <=(ContentId left, ContentId right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Determines whether one ContentId is greater than or equal to another.
    /// </summary>
    public static bool operator >=(ContentId left, ContentId right) => left.CompareTo(right) >= 0;
}