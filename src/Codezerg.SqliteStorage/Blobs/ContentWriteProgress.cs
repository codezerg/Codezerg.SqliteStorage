using System;

namespace Codezerg.SqliteStorage.Blobs
{
    /// <summary>
    /// Write progress information.
    /// </summary>
    public class ContentWriteProgress
    {
        public long BytesWritten { get; init; }
        public int ChunksWritten { get; init; }
        public DateTime StartedAt { get; init; }
        public TimeSpan Elapsed { get; init; }
        public double BytesPerSecond { get; init; }
    }
}
