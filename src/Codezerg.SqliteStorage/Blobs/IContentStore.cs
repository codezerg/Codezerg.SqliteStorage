using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Codezerg.SqliteStorage.Blobs
{
    /// <summary>
    /// Content-addressable storage with automatic chunking and deduplication.
    /// Thread-safe, async, streaming-optimized.
    /// </summary>
    public interface IContentStore : IDisposable
    {
        // === Write Operations ===

        /// <summary>
        /// Store content from stream. Automatically chunks and computes SHA256.
        /// Returns WriteResult with ContentId (generated upfront) and ContentHash (computed after).
        /// Deduplication happens internally via ContentHash.
        /// </summary>
        Task<ContentWriteResult> WriteAsync(
            Stream stream,
            string? extension = null,
            string? mimeType = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Store content from byte array.
        /// </summary>
        Task<ContentWriteResult> WriteAsync(
            byte[] data,
            string? extension = null,
            string? mimeType = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Begin streaming write for large content.
        /// Returns session with ContentId already assigned.
        /// Client can use ContentId to track upload progress.
        /// </summary>
        Task<IContentWriteSession> BeginWriteAsync(
            string? extension = null,
            string? mimeType = null,
            CancellationToken cancellationToken = default);

        // === Read Operations ===

        /// <summary>
        /// Read entire content as stream.
        /// Returns null if content not found.
        /// Stream is readable, seekable, and efficiently chunks on-demand.
        /// </summary>
        Task<Stream?> ReadAsync(
            ContentId contentId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Read content range (for resumable downloads).
        /// offset: Starting byte position (0-based)
        /// length: Number of bytes to read (null = read to end)
        /// </summary>
        Task<Stream?> ReadAsync(
            ContentId contentId,
            long offset,
            long? length = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Read entire content as byte array.
        /// Use only for small content to avoid memory pressure.
        /// </summary>
        Task<byte[]?> ReadAllAsync(
            ContentId contentId,
            CancellationToken cancellationToken = default);

        // === Metadata Operations ===

        /// <summary>
        /// Get content metadata without reading data.
        /// </summary>
        Task<ContentMetadata?> GetMetadataAsync(
            ContentId contentId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get content size in bytes. Returns -1 if not found.
        /// </summary>
        Task<long> GetSizeAsync(
            ContentId contentId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if content exists.
        /// </summary>
        Task<bool> ExistsAsync(
            ContentId contentId,
            CancellationToken cancellationToken = default);

        // === Delete Operations ===

        /// <summary>
        /// Delete content immediately.
        /// Automatically cleans up orphaned ContentHash and chunks in same transaction.
        /// Returns true if content was deleted, false if not found.
        /// </summary>
        Task<bool> DeleteAsync(
            ContentId contentId,
            CancellationToken cancellationToken = default);

        // === Integrity Operations ===

        /// <summary>
        /// Verify content integrity by recomputing hash.
        /// Returns true if content hash matches ContentId.
        /// </summary>
        Task<bool> VerifyIntegrityAsync(
            ContentId contentId,
            CancellationToken cancellationToken = default);
    }
}
