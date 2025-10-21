using System;

namespace Codezerg.SqliteStorage.Blobs;

/// <summary>
/// Chunk identifier - SHA256 hash of chunk data (32 bytes = 64 hex chars).
/// Immutable, equatable.
/// </summary>
public readonly struct ChunkId : IEquatable<ChunkId>
{
    private readonly string _hash; // 64-char lowercase hex

    private ChunkId(string hash)
    {
        if (hash.Length != 64)
            throw new ArgumentException("Hash must be 64 characters");
        _hash = hash;
    }

    public static ChunkId FromHash(byte[] sha256Hash)
    {
        if (sha256Hash.Length != 32)
            throw new ArgumentException("SHA256 hash must be 32 bytes");
        return new ChunkId(BytesToHex(sha256Hash));
    }

    public static ChunkId Parse(string hexString)
    {
        if (hexString.Length != 64)
            throw new ArgumentException("Hex string must be 64 characters");
        return new ChunkId(hexString.ToLowerInvariant());
    }

    public static bool TryParse(string? hexString, out ChunkId chunkId)
    {
        if (hexString?.Length == 64)
        {
            chunkId = new ChunkId(hexString.ToLowerInvariant());
            return true;
        }
        chunkId = default;
        return false;
    }

    private static byte[] HexToBytes(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }

    private static string BytesToHex(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    public byte[] ToBytes() => HexToBytes(_hash ?? new string('0', 64));
    public override string ToString() => _hash ?? new string('0', 64);

    public bool Equals(ChunkId other) => (_hash ?? "") == (other._hash ?? "");
    public override bool Equals(object? obj) => obj is ChunkId other && Equals(other);
    public override int GetHashCode() => (_hash ?? "").GetHashCode();

    public static bool operator ==(ChunkId left, ChunkId right) => left.Equals(right);
    public static bool operator !=(ChunkId left, ChunkId right) => !left.Equals(right);
}
