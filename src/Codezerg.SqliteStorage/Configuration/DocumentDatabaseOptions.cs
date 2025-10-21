using System;
using System.Collections.Generic;
using Codezerg.SqliteStorage.Configuration;

namespace Codezerg.SqliteStorage.Documents.Configuration;

/// <summary>
/// Configuration options for <see cref="IDocumentDatabase"/>.
/// </summary>
public class DocumentDatabaseOptions
{
    /// <summary>
    /// Gets or sets the shared SQLite storage options.
    /// If null, will use component-specific connection settings.
    /// </summary>
    public SqliteDatabaseOptions? Storage { get; set; }

    /// <summary>
    /// Gets or sets the connection string for the SQLite database.
    /// Only used if Storage is null. Otherwise, Storage.ConnectionString is used.
    /// </summary>
    public string ConnectionString { get; set; } = null!;

    /// <summary>
    /// Gets or sets the ADO.NET provider name.
    /// Only used if Storage is null. Otherwise, Storage.ProviderName is used.
    /// Default is "Microsoft.Data.Sqlite".
    /// </summary>
    public string ProviderName { get; set; } = "Microsoft.Data.Sqlite";

    /// <summary>
    /// Gets or sets the SQLite journal mode.
    /// Only used if Storage is null. Otherwise, Storage.JournalMode is used.
    /// Default is "WAL".
    /// </summary>
    public string? JournalMode { get; set; } = "WAL";

    /// <summary>
    /// Gets or sets the SQLite page size in bytes.
    /// Only used if Storage is null. Otherwise, Storage.PageSize is used.
    /// Default is 4096.
    /// </summary>
    public int? PageSize { get; set; } = 4096;

    /// <summary>
    /// Gets or sets the SQLite synchronous mode.
    /// Only used if Storage is null. Otherwise, Storage.Synchronous is used.
    /// Default is "NORMAL".
    /// </summary>
    public string? Synchronous { get; set; } = "NORMAL";

    /// <summary>
    /// Gets or sets whether to use JSONB (binary JSON) storage format.
    /// JSONB provides significant performance improvements (20-76% faster operations) with minimal storage overhead.
    /// Default is true (recommended). Set to false for legacy JSON text storage.
    /// </summary>
    public bool UseJsonB { get; set; } = true;

    /// <summary>
    /// List of predicate functions that determine if a collection should be cached
    /// </summary>
    internal List<Func<string, bool>> CachedCollectionPredicates { get; set; } = new();

    /// <summary>
    /// Gets the effective connection string (from Storage or direct).
    /// </summary>
    internal string GetConnectionString() => Storage?.ConnectionString ?? ConnectionString;

    /// <summary>
    /// Gets the effective provider name (from Storage or direct).
    /// </summary>
    internal string GetProviderName() => Storage?.ProviderName ?? ProviderName;

    /// <summary>
    /// Gets the effective journal mode (from Storage or direct).
    /// </summary>
    internal string? GetJournalMode() => Storage?.JournalMode ?? JournalMode;

    /// <summary>
    /// Gets the effective page size (from Storage or direct).
    /// </summary>
    internal int? GetPageSize() => Storage?.PageSize ?? PageSize;

    /// <summary>
    /// Gets the effective synchronous mode (from Storage or direct).
    /// </summary>
    internal string? GetSynchronous() => Storage?.Synchronous ?? Synchronous;

    /// <summary>
    /// Validates the options.
    /// </summary>
    internal void Validate()
    {
        var connectionString = GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "DocumentDatabaseOptions must specify a ConnectionString either directly or via Storage.");
        }

        var pageSize = GetPageSize();
        if (pageSize.HasValue)
        {
            var validPageSizes = new[] { 512, 1024, 2048, 4096, 8192, 16384, 32768, 65536 };
            if (Array.IndexOf(validPageSizes, pageSize.Value) == -1)
            {
                throw new InvalidOperationException(
                    $"PageSize must be one of: {string.Join(", ", validPageSizes)}");
            }
        }
    }
}