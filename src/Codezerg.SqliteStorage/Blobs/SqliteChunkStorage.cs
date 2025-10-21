using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Codezerg.SqliteStorage.Blobs
{
    /// <summary>
    /// SQLite-based chunk storage implementation.
    /// Stores chunks as BLOBs in the database alongside metadata.
    /// Provides single-file deployment with transactional consistency.
    /// </summary>
    public class SqliteChunkStorage : IChunkStorage
    {
        private readonly ISqliteConnectionProvider _sqlite;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
        private bool _initialized = false;

        /// <summary>
        /// Create a new SQLite chunk storage.
        /// </summary>
        public SqliteChunkStorage(ISqliteConnectionProvider sqlite)
        {
            _sqlite = sqlite ?? throw new ArgumentNullException(nameof(sqlite));
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_initialized) return;

            await _initLock.WaitAsync(cancellationToken);
            try
            {
                if (_initialized) return;

                using var connection = _sqlite.CreateConnection();
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS chunk_storage (
                        chunk_id TEXT PRIMARY KEY,
                        data BLOB NOT NULL,
                        size INTEGER NOT NULL,
                        created_at TEXT NOT NULL
                    );

                    CREATE INDEX IF NOT EXISTS idx_chunk_storage_created_at ON chunk_storage(created_at);
                ";
                await cmd.ExecuteNonQueryAsync(cancellationToken);

                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task WriteChunkAsync(
            ChunkId chunkId,
            byte[] data,
            CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken);

            using var connection = _sqlite.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            // Check if chunk already exists (idempotent)
            using (var checkCmd = connection.CreateCommand())
            {
                checkCmd.CommandText = "SELECT COUNT(*) FROM chunk_storage WHERE chunk_id = @chunkId";
                checkCmd.AddParameterWithValue("@chunkId", chunkId.ToString());

                var count = (long)(await checkCmd.ExecuteScalarAsync(cancellationToken) ?? 0L);
                if (count > 0)
                    return; // Already exists, idempotent behavior
            }

            // Insert new chunk
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT OR IGNORE INTO chunk_storage (chunk_id, data, size, created_at)
                    VALUES (@chunkId, @data, @size, @createdAt)
                ";
                cmd.AddParameterWithValue("@chunkId", chunkId.ToString());
                cmd.AddParameterWithValue("@data", data);
                cmd.AddParameterWithValue("@size", data.Length);
                cmd.AddParameterWithValue("@createdAt", DateTime.UtcNow.ToString("O"));

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        public async Task<byte[]?> ReadChunkAsync(
            ChunkId chunkId,
            CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken);

            using var connection = _sqlite.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT data FROM chunk_storage WHERE chunk_id = @chunkId";
            cmd.AddParameterWithValue("@chunkId", chunkId.ToString());

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            if (reader.IsDBNull(0))
                return null;

            // Read BLOB data
            var size = reader.GetBytes(0, 0, null, 0, 0);
            var data = new byte[size];
            reader.GetBytes(0, 0, data, 0, (int)size);

            return data;
        }

        public async Task<bool> ExistsAsync(
            ChunkId chunkId,
            CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken);

            using var connection = _sqlite.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM chunk_storage WHERE chunk_id = @chunkId";
            cmd.AddParameterWithValue("@chunkId", chunkId.ToString());

            var count = (long)(await cmd.ExecuteScalarAsync(cancellationToken) ?? 0L);
            return count > 0;
        }

        public async Task DeleteChunkAsync(
            ChunkId chunkId,
            CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken);

            using var connection = _sqlite.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM chunk_storage WHERE chunk_id = @chunkId";
            cmd.AddParameterWithValue("@chunkId", chunkId.ToString());

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task DeleteChunksAsync(
            IEnumerable<ChunkId> chunkIds,
            CancellationToken cancellationToken = default)
        {
            await InitializeAsync(cancellationToken);

            var chunkIdList = chunkIds.ToList();
            if (!chunkIdList.Any())
                return;

            using var connection = _sqlite.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var transaction = connection.BeginTransaction();
            try
            {
                foreach (var chunkId in chunkIdList)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM chunk_storage WHERE chunk_id = @chunkId";
                    cmd.AddParameterWithValue("@chunkId", chunkId.ToString());
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public void Dispose()
        {
            _initLock?.Dispose();
        }
    }
}
