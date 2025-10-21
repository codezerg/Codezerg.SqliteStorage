using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Codezerg.SqliteStorage.Blobs
{
    /// <summary>
    /// Streaming write session for large content uploads.
    /// </summary>
    public interface IContentWriteSession : IDisposable
    {
        /// <summary>
        /// ContentId assigned to this upload (known upfront).
        /// Client uses this to reference the upload before completion.
        /// </summary>
        ContentId ContentId { get; }

        /// <summary>
        /// File extension (e.g., ".pdf", ".jpg").
        /// </summary>
        string? Extension { get; }

        /// <summary>
        /// MIME type (e.g., "application/pdf", "image/jpeg").
        /// </summary>
        string? MimeType { get; }

        /// <summary>
        /// Append data to content. Can be called multiple times.
        /// </summary>
        Task AppendAsync(
            byte[] data,
            int offset,
            int count,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Append from stream.
        /// </summary>
        Task AppendAsync(
            Stream stream,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Complete write and return ContentWriteResult.
        /// Commits transaction and makes content available.
        /// ContentHash is computed at this point.
        /// </summary>
        Task<ContentWriteResult> CompleteAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Abort write and rollback.
        /// Cleans up any partial data.
        /// </summary>
        Task AbortAsync();

        /// <summary>
        /// Current progress information.
        /// </summary>
        ContentWriteProgress Progress { get; }
    }
}
