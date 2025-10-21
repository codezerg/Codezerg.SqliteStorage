using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Codezerg.SqliteStorage.Blobs
{
    /// <summary>
    /// Simple abstraction for chunk storage.
    /// Just stores and retrieves chunks by ID - nothing else.
    /// All metadata/deduplication logic is handled by ContentStore.
    /// </summary>
    public interface IChunkStorage : IDisposable
    {
        /// <summary>
        /// Write a chunk to storage.
        /// If chunk already exists, does nothing (idempotent).
        /// </summary>
        Task WriteChunkAsync(
            ChunkId chunkId,
            byte[] data,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Read chunk data from storage.
        /// Returns null if chunk not found.
        /// </summary>
        Task<byte[]?> ReadChunkAsync(
            ChunkId chunkId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if chunk exists in storage.
        /// </summary>
        Task<bool> ExistsAsync(
            ChunkId chunkId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete chunk from storage.
        /// If chunk doesn't exist, does nothing (idempotent).
        /// </summary>
        Task DeleteChunkAsync(
            ChunkId chunkId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete multiple chunks in one call.
        /// More efficient than calling DeleteChunkAsync multiple times.
        /// </summary>
        Task DeleteChunksAsync(
            IEnumerable<ChunkId> chunkIds,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Initialize storage (create directories, verify permissions, etc.).
        /// Called once at startup.
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken = default);
    }
}
