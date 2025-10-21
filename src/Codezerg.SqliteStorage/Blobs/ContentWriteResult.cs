namespace Codezerg.SqliteStorage.Blobs
{
    /// <summary>
    /// Result of write operation.
    /// </summary>
    public class ContentWriteResult
    {
        public ContentId ContentId { get; init; }         // Generated ID (known upfront)
        public string ContentHash { get; init; } = string.Empty;  // SHA256 hash (64 hex chars)
        public long Size { get; init; }
        public int ChunkCount { get; init; }
        public bool WasDeduplicated { get; init; }        // True if content already existed
        public string? Extension { get; init; }           // File extension (e.g., ".pdf")
        public string? MimeType { get; init; }            // MIME type (e.g., "application/pdf")
    }
}
