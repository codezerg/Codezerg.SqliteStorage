using System;

namespace Codezerg.SqliteStorage.Blobs;

/// <summary>
/// Content metadata.
/// </summary>
public class ContentMetadata
{
    public ContentId ContentId { get; init; }
    public string ContentHash { get; init; } = string.Empty;  // SHA256 hash for deduplication
    public long Size { get; init; }
    public int ChunkCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastAccessedAt { get; init; }
    public string? Extension { get; init; }           // File extension (e.g., ".pdf")
    public string? MimeType { get; init; }            // MIME type (e.g., "application/pdf")
}
