using System;
using Codezerg.SqliteStorage.Blobs;

namespace Codezerg.SqliteStorage.Configuration
{
    /// <summary>
    /// Content store configuration options.
    /// </summary>
    public class ContentStoreOptions
    {
        /// <summary>
        /// Chunk storage provider.
        /// If not specified, defaults to SqliteChunkStorage (stores chunks in the same database).
        /// Alternative: FileChunkStorage for file system storage.
        /// </summary>
        public IChunkStorage? ChunkStorage { get; set; }

        /// <summary>
        /// Chunk size in bytes.
        /// Default: 1MB. Range: 256KB - 16MB.
        /// </summary>
        public int ChunkSize { get; set; } = 1024 * 1024;

   
        /// <summary>
        /// Validate options.
        /// </summary>
        public void Validate()
        {
        }
    }
}
