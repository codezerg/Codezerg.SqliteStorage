using Codezerg.SqliteStorage.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Codezerg.SqliteStorage.Blobs
{
    /// <summary>
    /// SQLite-based content store implementation with automatic chunking and deduplication.
    /// Thread-safe, async, streaming-optimized.
    /// </summary>
    public class SqliteContentStore : IContentStore
    {
        private readonly ISqliteConnectionProvider _sqlite;
        internal readonly ContentStoreOptions _options;
        private readonly IChunkStorage _chunkStorage;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
        private bool _initialized = false;

        public SqliteContentStore(ISqliteConnectionProvider sqlite, ContentStoreOptions options)
        {
            _sqlite = sqlite ?? throw new ArgumentNullException(nameof(sqlite));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _options.Validate();
            _chunkStorage = _options.ChunkStorage ?? throw new InvalidOperationException("ChunkStorage is required");
        }

        private async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
        {
            if (_initialized) return;

            await _initLock.WaitAsync(cancellationToken);
            try
            {
                if (_initialized) return;

                await InitializeDatabaseAsync(cancellationToken);
                await _chunkStorage.InitializeAsync(cancellationToken);
                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private async Task InitializeDatabaseAsync(CancellationToken cancellationToken)
        {
            using var connection = _sqlite.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                -- Content metadata table
                CREATE TABLE IF NOT EXISTS content_metadata (
                    content_id TEXT PRIMARY KEY,
                    content_hash TEXT NOT NULL,
                    size INTEGER NOT NULL,
                    chunk_count INTEGER NOT NULL,
                    extension TEXT,
                    mime_type TEXT,
                    created_at TEXT NOT NULL,
                    last_accessed_at TEXT NOT NULL
                );

                -- Index on content_hash for deduplication
                CREATE INDEX IF NOT EXISTS idx_content_hash ON content_metadata(content_hash);

                -- Index on extension for querying
                CREATE INDEX IF NOT EXISTS idx_extension ON content_metadata(extension);

                -- Index on mime_type for querying
                CREATE INDEX IF NOT EXISTS idx_mime_type ON content_metadata(mime_type);

                -- Content hash metadata (deduplication layer)
                CREATE TABLE IF NOT EXISTS content_hash_metadata (
                    content_hash TEXT PRIMARY KEY,
                    size INTEGER NOT NULL,
                    chunk_count INTEGER NOT NULL,
                    created_at TEXT NOT NULL
                );

                -- Chunk metadata table
                CREATE TABLE IF NOT EXISTS chunk_metadata (
                    chunk_id TEXT NOT NULL,
                    content_hash TEXT NOT NULL,
                    chunk_index INTEGER NOT NULL,
                    chunk_size INTEGER NOT NULL,
                    PRIMARY KEY (content_hash, chunk_index),
                    FOREIGN KEY (content_hash) REFERENCES content_hash_metadata(content_hash)
                );

                -- Index on chunk_id for orphan detection
                CREATE INDEX IF NOT EXISTS idx_chunk_id ON chunk_metadata(chunk_id);
            ";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // === Write Operations ===

        public async Task<ContentWriteResult> WriteAsync(
            Stream stream,
            string? extension = null,
            string? mimeType = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync(cancellationToken);

            var session = await BeginWriteAsync(extension, mimeType, cancellationToken);
            try
            {
                await session.AppendAsync(stream, cancellationToken);
                return await session.CompleteAsync(cancellationToken);
            }
            catch
            {
                await session.AbortAsync();
                throw;
            }
        }

        public async Task<ContentWriteResult> WriteAsync(
            byte[] data,
            string? extension = null,
            string? mimeType = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync(cancellationToken);

            using var stream = new MemoryStream(data);
            return await WriteAsync(stream, extension, mimeType, cancellationToken);
        }

        public async Task<IContentWriteSession> BeginWriteAsync(
            string? extension = null,
            string? mimeType = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync(cancellationToken);

            var contentId = ContentId.NewId();
            return new ContentWriteSession(this, contentId, extension, mimeType);
        }

        // === Read Operations ===

        public async Task<Stream?> ReadAsync(
            ContentId contentId,
            CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync(cancellationToken);

            var metadata = await GetMetadataAsync(contentId, cancellationToken);
            if (metadata == null)
                return null;

            return new ContentStream(this, metadata);
        }

        public async Task<Stream?> ReadAsync(
            ContentId contentId,
            long offset,
            long? length = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync(cancellationToken);

            var stream = await ReadAsync(contentId, cancellationToken);
            if (stream == null)
                return null;

            stream.Seek(offset, SeekOrigin.Begin);

            if (length.HasValue)
            {
                return new BoundedStream(stream, length.Value);
            }

            return stream;
        }

        public async Task<byte[]?> ReadAllAsync(
            ContentId contentId,
            CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync(cancellationToken);

            var stream = await ReadAsync(contentId, cancellationToken);
            if (stream == null)
                return null;

            using (stream)
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, 81920, cancellationToken);
                return ms.ToArray();
            }
        }

        // === Metadata Operations ===

        public async Task<ContentMetadata?> GetMetadataAsync(
            ContentId contentId,
            CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync(cancellationToken);

            using var connection = _sqlite.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            ContentMetadata? metadata = null;

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT content_id, content_hash, size, chunk_count, extension, mime_type, created_at, last_accessed_at
                    FROM content_metadata
                    WHERE content_id = @contentId
                ";
                cmd.AddParameterWithValue("@contentId", contentId.ToString());

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    return null;

                metadata = new ContentMetadata
                {
                    ContentId = ContentId.Parse(reader.GetString(0)),
                    ContentHash = reader.GetString(1),
                    Size = reader.GetInt64(2),
                    ChunkCount = reader.GetInt32(3),
                    Extension = reader.IsDBNull(4) ? null : reader.GetString(4),
                    MimeType = reader.IsDBNull(5) ? null : reader.GetString(5),
                    CreatedAt = DateTime.Parse(reader.GetString(6)),
                    LastAccessedAt = DateTime.Parse(reader.GetString(7))
                };
            }

            // Update last accessed time after closing the reader
            if (metadata != null)
            {
                using var updateCmd = connection.CreateCommand();
                updateCmd.CommandText = @"
                    UPDATE content_metadata
                    SET last_accessed_at = @lastAccessedAt
                    WHERE content_id = @contentId
                ";
                updateCmd.AddParameterWithValue("@lastAccessedAt", DateTime.UtcNow.ToString("O"));
                updateCmd.AddParameterWithValue("@contentId", contentId.ToString());
                await updateCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            return metadata;
        }

        public async Task<long> GetSizeAsync(
            ContentId contentId,
            CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync(cancellationToken);

            var metadata = await GetMetadataAsync(contentId, cancellationToken);
            return metadata?.Size ?? -1;
        }

        public async Task<bool> ExistsAsync(
            ContentId contentId,
            CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync(cancellationToken);

            var metadata = await GetMetadataAsync(contentId, cancellationToken);
            return metadata != null;
        }

        // === Delete Operations ===

        public async Task<bool> DeleteAsync(
            ContentId contentId,
            CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync(cancellationToken);

            // Get content hash before deletion
            string? contentHash = null;
            using (var connection = _sqlite.CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT content_hash FROM content_metadata WHERE content_id = @contentId";
                cmd.AddParameterWithValue("@contentId", contentId.ToString());
                contentHash = await cmd.ExecuteScalarAsync(cancellationToken) as string;
            }

            if (contentHash == null)
                return false;

            // Delete content metadata
            using (var connection = _sqlite.CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM content_metadata WHERE content_id = @contentId";
                cmd.AddParameterWithValue("@contentId", contentId.ToString());
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Check if content_hash is orphaned
            bool hashOrphaned = false;
            using (var connection = _sqlite.CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM content_metadata WHERE content_hash = @contentHash";
                cmd.AddParameterWithValue("@contentHash", contentHash);
                var count = (long)(await cmd.ExecuteScalarAsync(cancellationToken) ?? 0L);
                hashOrphaned = count == 0;
            }

            if (hashOrphaned)
            {
                // Get orphaned chunks
                var orphanedChunks = new List<ChunkId>();
                using (var connection = _sqlite.CreateConnection())
                {
                    await connection.OpenAsync(cancellationToken);
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT chunk_id FROM chunk_metadata WHERE content_hash = @contentHash";
                    cmd.AddParameterWithValue("@contentHash", contentHash);
                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        orphanedChunks.Add(ChunkId.Parse(reader.GetString(0)));
                    }
                }

                // Delete chunk metadata
                using (var connection = _sqlite.CreateConnection())
                {
                    await connection.OpenAsync(cancellationToken);
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "DELETE FROM chunk_metadata WHERE content_hash = @contentHash";
                    cmd.AddParameterWithValue("@contentHash", contentHash);
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                // Delete content hash metadata
                using (var connection = _sqlite.CreateConnection())
                {
                    await connection.OpenAsync(cancellationToken);
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "DELETE FROM content_hash_metadata WHERE content_hash = @contentHash";
                    cmd.AddParameterWithValue("@contentHash", contentHash);
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                // Find truly orphaned chunks (not referenced by any other content_hash)
                var trulyOrphanedChunks = new List<ChunkId>();
                foreach (var chunkId in orphanedChunks)
                {
                    using var connection = _sqlite.CreateConnection();
                    await connection.OpenAsync(cancellationToken);
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(*) FROM chunk_metadata WHERE chunk_id = @chunkId";
                    cmd.AddParameterWithValue("@chunkId", chunkId.ToString());
                    var count = (long)(await cmd.ExecuteScalarAsync(cancellationToken) ?? 0L);
                    if (count == 0)
                    {
                        trulyOrphanedChunks.Add(chunkId);
                    }
                }

                // Delete orphaned chunks from storage
                if (trulyOrphanedChunks.Any())
                {
                    await _chunkStorage.DeleteChunksAsync(trulyOrphanedChunks, cancellationToken);
                }
            }

            return true;
        }

        // === Integrity Operations ===

        public async Task<bool> VerifyIntegrityAsync(
            ContentId contentId,
            CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync(cancellationToken);

            var metadata = await GetMetadataAsync(contentId, cancellationToken);
            if (metadata == null)
                return false;

            // Read content and compute hash
            var stream = await ReadAsync(contentId, cancellationToken);
            if (stream == null)
                return false;

            using (stream)
            using (var sha256 = SHA256.Create())
            {
                var hash = await ComputeHashAsync(stream, sha256, cancellationToken);
                var hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                return hashString == metadata.ContentHash;
            }
        }

        // === Internal Methods ===

        internal async Task<List<ChunkId>> GetChunkIdsAsync(string contentHash, CancellationToken cancellationToken)
        {
            using var connection = _sqlite.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT chunk_id
                FROM chunk_metadata
                WHERE content_hash = @contentHash
                ORDER BY chunk_index
            ";
            cmd.AddParameterWithValue("@contentHash", contentHash);

            var chunks = new List<ChunkId>();
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                chunks.Add(ChunkId.Parse(reader.GetString(0)));
            }

            return chunks;
        }

        internal async Task<byte[]?> ReadChunkAsync(ChunkId chunkId, CancellationToken cancellationToken)
        {
            return await _chunkStorage.ReadChunkAsync(chunkId, cancellationToken);
        }

        internal async Task<ContentWriteResult> CompleteWriteSessionAsync(
            ContentId contentId,
            string? extension,
            string? mimeType,
            List<(ChunkId chunkId, byte[] data)> chunks,
            CancellationToken cancellationToken)
        {
            // Compute overall content hash
            using var sha256 = SHA256.Create();
            using var ms = new MemoryStream();
            foreach (var (_, data) in chunks)
            {
                await ms.WriteAsync(data, 0, data.Length, cancellationToken);
            }
            ms.Position = 0;
            var contentHashBytes = await ComputeHashAsync(ms, sha256, cancellationToken);
            var contentHash = BitConverter.ToString(contentHashBytes).Replace("-", "").ToLowerInvariant();

            long totalSize = chunks.Sum(c => (long)c.data.Length);

            // Check if content_hash already exists (deduplication)
            bool wasDeduplicated = false;
            using (var connection = _sqlite.CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM content_hash_metadata WHERE content_hash = @contentHash";
                cmd.AddParameterWithValue("@contentHash", contentHash);
                var count = (long)(await cmd.ExecuteScalarAsync(cancellationToken) ?? 0L);
                wasDeduplicated = count > 0;
            }

            if (!wasDeduplicated)
            {
                // Write chunks to storage first
                for (int i = 0; i < chunks.Count; i++)
                {
                    var (chunkId, data) = chunks[i];
                    await _chunkStorage.WriteChunkAsync(chunkId, data, cancellationToken);
                }

                // Insert content_hash_metadata
                using (var connection = _sqlite.CreateConnection())
                {
                    await connection.OpenAsync(cancellationToken);
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        INSERT INTO content_hash_metadata (content_hash, size, chunk_count, created_at)
                        VALUES (@contentHash, @size, @chunkCount, @createdAt)
                    ";
                    cmd.AddParameterWithValue("@contentHash", contentHash);
                    cmd.AddParameterWithValue("@size", totalSize);
                    cmd.AddParameterWithValue("@chunkCount", chunks.Count);
                    cmd.AddParameterWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                // Insert chunk_metadata
                for (int i = 0; i < chunks.Count; i++)
                {
                    var (chunkId, data) = chunks[i];
                    using var connection = _sqlite.CreateConnection();
                    await connection.OpenAsync(cancellationToken);
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        INSERT INTO chunk_metadata (chunk_id, content_hash, chunk_index, chunk_size)
                        VALUES (@chunkId, @contentHash, @chunkIndex, @chunkSize)
                    ";
                    cmd.AddParameterWithValue("@chunkId", chunkId.ToString());
                    cmd.AddParameterWithValue("@contentHash", contentHash);
                    cmd.AddParameterWithValue("@chunkIndex", i);
                    cmd.AddParameterWithValue("@chunkSize", data.Length);
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            // Insert content_metadata
            using (var connection = _sqlite.CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO content_metadata (content_id, content_hash, size, chunk_count, extension, mime_type, created_at, last_accessed_at)
                    VALUES (@contentId, @contentHash, @size, @chunkCount, @extension, @mimeType, @createdAt, @lastAccessedAt)
                ";
                cmd.AddParameterWithValue("@contentId", contentId.ToString());
                cmd.AddParameterWithValue("@contentHash", contentHash);
                cmd.AddParameterWithValue("@size", totalSize);
                cmd.AddParameterWithValue("@chunkCount", chunks.Count);
                cmd.AddParameterWithValue("@extension", extension ?? (object)DBNull.Value);
                cmd.AddParameterWithValue("@mimeType", mimeType ?? (object)DBNull.Value);
                cmd.AddParameterWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
                cmd.AddParameterWithValue("@lastAccessedAt", DateTime.UtcNow.ToString("O"));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            return new ContentWriteResult
            {
                ContentId = contentId,
                ContentHash = contentHash,
                Size = totalSize,
                ChunkCount = chunks.Count,
                WasDeduplicated = wasDeduplicated,
                Extension = extension,
                MimeType = mimeType
            };
        }

        private async Task UpdateLastAccessedAsync(ContentId contentId, CancellationToken cancellationToken)
        {
            using var connection = _sqlite.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE content_metadata
                SET last_accessed_at = @lastAccessedAt
                WHERE content_id = @contentId
            ";
            cmd.AddParameterWithValue("@lastAccessedAt", DateTime.UtcNow.ToString("O"));
            cmd.AddParameterWithValue("@contentId", contentId.ToString());
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<byte[]> ComputeHashAsync(Stream stream, SHA256 sha256, CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
            }
            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return sha256.Hash ?? Array.Empty<byte>();
        }

        public void Dispose()
        {
            _chunkStorage?.Dispose();
            _initLock?.Dispose();
        }

        // === Helper Classes ===

        private class ContentStream : Stream
        {
            private readonly SqliteContentStore _store;
            private readonly ContentMetadata _metadata;
            private readonly List<ChunkId> _chunkIds;
            private long _position;
            private int _currentChunkIndex = -1;
            private byte[]? _currentChunkData;
            private int _currentChunkPosition;

            public ContentStream(SqliteContentStore store, ContentMetadata metadata)
            {
                _store = store;
                _metadata = metadata;
                _chunkIds = _store.GetChunkIdsAsync(metadata.ContentHash, CancellationToken.None).GetAwaiter().GetResult();
            }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => _metadata.Size;
            public override long Position
            {
                get => _position;
                set => Seek(value, SeekOrigin.Begin);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (_position >= _metadata.Size)
                    return 0;

                int totalBytesRead = 0;

                while (count > 0 && _position < _metadata.Size)
                {
                    // Calculate which chunk contains the current position
                    long chunkSize = _store._options.ChunkSize;
                    int targetChunkIndex = (int)(_position / chunkSize);

                    // Load chunk if needed (either first time or we moved to a new chunk)
                    if (_currentChunkData == null || _currentChunkIndex != targetChunkIndex)
                    {
                        if (targetChunkIndex >= _chunkIds.Count)
                            break;

                        _currentChunkIndex = targetChunkIndex;
                        _currentChunkData = await _store.ReadChunkAsync(_chunkIds[_currentChunkIndex], cancellationToken);

                        // Calculate position within this chunk
                        _currentChunkPosition = (int)(_position % chunkSize);
                    }

                    // Read from current chunk
                    int bytesToRead = Math.Min(count, _currentChunkData!.Length - _currentChunkPosition);
                    bytesToRead = Math.Min(bytesToRead, (int)(_metadata.Size - _position));

                    Array.Copy(_currentChunkData, _currentChunkPosition, buffer, offset, bytesToRead);

                    _currentChunkPosition += bytesToRead;
                    _position += bytesToRead;
                    offset += bytesToRead;
                    count -= bytesToRead;
                    totalBytesRead += bytesToRead;
                }

                return totalBytesRead;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                long newPosition = origin switch
                {
                    SeekOrigin.Begin => offset,
                    SeekOrigin.Current => _position + offset,
                    SeekOrigin.End => _metadata.Size + offset,
                    _ => throw new ArgumentException("Invalid seek origin")
                };

                if (newPosition < 0)
                    throw new ArgumentException("Cannot seek before beginning");

                _position = newPosition;

                // Reset chunk state - next read will load the correct chunk
                _currentChunkIndex = -1;
                _currentChunkData = null;
                _currentChunkPosition = 0;

                return _position;
            }

            public override void Flush() { }
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private class BoundedStream : Stream
        {
            private readonly Stream _baseStream;
            private readonly long _length;
            private long _position;

            public BoundedStream(Stream baseStream, long length)
            {
                _baseStream = baseStream;
                _length = length;
            }

            public override bool CanRead => true;
            public override bool CanSeek => _baseStream.CanSeek;
            public override bool CanWrite => false;
            public override long Length => _length;
            public override long Position
            {
                get => _position;
                set => Seek(value, SeekOrigin.Begin);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int bytesToRead = (int)Math.Min(count, _length - _position);
                if (bytesToRead <= 0)
                    return 0;

                int bytesRead = _baseStream.Read(buffer, offset, bytesToRead);
                _position += bytesRead;
                return bytesRead;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                int bytesToRead = (int)Math.Min(count, _length - _position);
                if (bytesToRead <= 0)
                    return 0;

                int bytesRead = await _baseStream.ReadAsync(buffer, offset, bytesToRead, cancellationToken);
                _position += bytesRead;
                return bytesRead;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                long newPosition = origin switch
                {
                    SeekOrigin.Begin => offset,
                    SeekOrigin.Current => _position + offset,
                    SeekOrigin.End => _length + offset,
                    _ => throw new ArgumentException("Invalid seek origin")
                };

                if (newPosition < 0)
                    throw new ArgumentException("Cannot seek before beginning");

                _position = newPosition;
                return _position;
            }

            public override void Flush() => _baseStream.Flush();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _baseStream?.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}
