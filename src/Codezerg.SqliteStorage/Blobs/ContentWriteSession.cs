using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Codezerg.SqliteStorage.Blobs
{
    /// <summary>
    /// Implementation of streaming write session for large content uploads.
    /// </summary>
    internal class ContentWriteSession : IContentWriteSession
    {
        private readonly SqliteContentStore _store;
        private readonly ContentId _contentId;
        private readonly string? _extension;
        private readonly string? _mimeType;
        private readonly int _chunkSize;
        private readonly List<(ChunkId chunkId, byte[] data)> _chunks = new List<(ChunkId, byte[])>();
        private readonly DateTime _startedAt;
        private long _bytesWritten;
        private MemoryStream _currentChunk;
        private bool _completed;
        private bool _aborted;

        public ContentWriteSession(
            SqliteContentStore store,
            ContentId contentId,
            string? extension,
            string? mimeType)
        {
            _store = store;
            _contentId = contentId;
            _extension = extension;
            _mimeType = mimeType;
            _chunkSize = store._options.ChunkSize;
            _currentChunk = new MemoryStream();
            _startedAt = DateTime.UtcNow;
        }

        public ContentId ContentId => _contentId;
        public string? Extension => _extension;
        public string? MimeType => _mimeType;

        public ContentWriteProgress Progress
        {
            get
            {
                var elapsed = DateTime.UtcNow - _startedAt;
                var bytesPerSecond = elapsed.TotalSeconds > 0
                    ? _bytesWritten / elapsed.TotalSeconds
                    : 0;

                return new ContentWriteProgress
                {
                    BytesWritten = _bytesWritten,
                    ChunksWritten = _chunks.Count,
                    StartedAt = _startedAt,
                    Elapsed = elapsed,
                    BytesPerSecond = bytesPerSecond
                };
            }
        }

        public async Task AppendAsync(
            byte[] data,
            int offset,
            int count,
            CancellationToken cancellationToken = default)
        {
            if (_completed)
                throw new InvalidOperationException("Session already completed");
            if (_aborted)
                throw new InvalidOperationException("Session aborted");

            int remaining = count;
            int currentOffset = offset;

            while (remaining > 0)
            {
                int spaceInChunk = _chunkSize - (int)_currentChunk.Length;
                int bytesToWrite = Math.Min(remaining, spaceInChunk);

                await _currentChunk.WriteAsync(data, currentOffset, bytesToWrite, cancellationToken);
                _bytesWritten += bytesToWrite;
                currentOffset += bytesToWrite;
                remaining -= bytesToWrite;

                // If chunk is full, finalize it
                if (_currentChunk.Length >= _chunkSize)
                {
                    await FinalizeCurrentChunkAsync(cancellationToken);
                }
            }
        }

        public async Task AppendAsync(
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            if (_completed)
                throw new InvalidOperationException("Session already completed");
            if (_aborted)
                throw new InvalidOperationException("Session aborted");

            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await AppendAsync(buffer, 0, bytesRead, cancellationToken);
            }
        }

        public async Task<ContentWriteResult> CompleteAsync(
            CancellationToken cancellationToken = default)
        {
            if (_completed)
                throw new InvalidOperationException("Session already completed");
            if (_aborted)
                throw new InvalidOperationException("Session aborted");

            // Finalize last chunk if it has any data
            if (_currentChunk.Length > 0)
            {
                await FinalizeCurrentChunkAsync(cancellationToken);
            }

            _completed = true;

            // Complete the write in the store
            var result = await _store.CompleteWriteSessionAsync(
                _contentId,
                _extension,
                _mimeType,
                _chunks,
                cancellationToken);

            return result;
        }

        public Task AbortAsync()
        {
            _aborted = true;
            _chunks.Clear();
            _currentChunk?.Dispose();
            _currentChunk = new MemoryStream();
            return Task.CompletedTask;
        }

        private async Task FinalizeCurrentChunkAsync(CancellationToken cancellationToken)
        {
            if (_currentChunk.Length == 0)
                return;

            var chunkData = _currentChunk.ToArray();

            // Compute chunk hash
            using var sha256 = SHA256.Create();
            var chunkHash = sha256.ComputeHash(chunkData);
            var chunkId = ChunkId.FromHash(chunkHash);

            _chunks.Add((chunkId, chunkData));

            // Reset current chunk
            _currentChunk.Dispose();
            _currentChunk = new MemoryStream();

            await Task.CompletedTask;
        }

        public void Dispose()
        {
            _currentChunk?.Dispose();
        }
    }
}
