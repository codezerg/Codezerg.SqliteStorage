using Codezerg.SqliteStorage.Common;
using System;

namespace Codezerg.SqliteStorage;

/// <summary>
/// Represents a unique identifier for a document.
/// </summary>
public readonly struct DocumentId : IEquatable<DocumentId>, IComparable<DocumentId>
{
    private readonly byte[] _value;

    /// <summary>
    /// Gets an empty DocumentId.
    /// </summary>
    public static DocumentId Empty => new(new byte[12]);

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentId"/> struct.
    /// </summary>
    public DocumentId()
    {
        _value = ObjectId.Generate();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentId"/> struct from a byte array.
    /// </summary>
    /// <param name="value">The byte array representing the ID.</param>
    public DocumentId(byte[] value)
    {
        if (value == null || value.Length != 12)
            throw new ArgumentException("DocumentId must be exactly 12 bytes", nameof(value));

        _value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentId"/> struct from a hex string.
    /// </summary>
    /// <param name="hexString">The hex string representing the ID.</param>
    public DocumentId(string hexString)
    {
        if (string.IsNullOrEmpty(hexString) || hexString.Length != 24)
            throw new ArgumentException("DocumentId hex string must be exactly 24 characters", nameof(hexString));

        _value = ObjectId.FromHexString(hexString);
    }

    /// <summary>
    /// Gets the timestamp component of this DocumentId.
    /// </summary>
    public DateTime Timestamp => ObjectId.GetCreationTime(_value);

    /// <summary>
    /// Generates a new DocumentId.
    /// </summary>
    /// <returns>A new unique DocumentId.</returns>
    public static DocumentId NewId() => new();

    /// <summary>
    /// Parses a hex string into a DocumentId.
    /// </summary>
    public static DocumentId Parse(string hexString) => new(hexString);

    /// <summary>
    /// Tries to parse a hex string into a DocumentId.
    /// </summary>
    public static bool TryParse(string? hexString, out DocumentId id)
    {
        if (string.IsNullOrEmpty(hexString) || hexString!.Length != 24)
        {
            id = Empty;
            return false;
        }

        try
        {
            id = new DocumentId(hexString);
            return true;
        }
        catch
        {
            id = Empty;
            return false;
        }
    }

    /// <summary>
    /// Converts the DocumentId to a hex string.
    /// </summary>
    public override string ToString() => _value == null ? string.Empty : ObjectId.ToHexString(_value);

    /// <summary>
    /// Gets the byte array representation of this DocumentId.
    /// </summary>
    public byte[] ToByteArray() => _value ?? Array.Empty<byte>();

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DocumentId id && Equals(id);

    /// <inheritdoc/>
    public bool Equals(DocumentId other) => ObjectId.BytesEqual(_value, other._value);

    /// <inheritdoc/>
    public override int GetHashCode() => ObjectId.ComputeHashCode(_value);

    /// <inheritdoc/>
    public int CompareTo(DocumentId other) => ObjectId.BytesCompare(_value, other._value);

    /// <summary>
    /// Determines whether two DocumentId instances are equal.
    /// </summary>
    public static bool operator ==(DocumentId left, DocumentId right) => left.Equals(right);

    /// <summary>
    /// Determines whether two DocumentId instances are not equal.
    /// </summary>
    public static bool operator !=(DocumentId left, DocumentId right) => !left.Equals(right);

    /// <summary>
    /// Determines whether one DocumentId is less than another.
    /// </summary>
    public static bool operator <(DocumentId left, DocumentId right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Determines whether one DocumentId is greater than another.
    /// </summary>
    public static bool operator >(DocumentId left, DocumentId right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Determines whether one DocumentId is less than or equal to another.
    /// </summary>
    public static bool operator <=(DocumentId left, DocumentId right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Determines whether one DocumentId is greater than or equal to another.
    /// </summary>
    public static bool operator >=(DocumentId left, DocumentId right) => left.CompareTo(right) >= 0;
}
