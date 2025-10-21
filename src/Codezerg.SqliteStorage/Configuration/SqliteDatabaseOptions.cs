using System;

namespace Codezerg.SqliteStorage.Configuration;

public class SqliteDatabaseOptions
{
    /// <summary>
    /// Gets or sets the ADO.NET provider name for the database connection.
    /// Default is "Microsoft.Data.Sqlite".
    /// </summary>
    public string ProviderName { get; set; } = "Microsoft.Data.Sqlite";

    /// <summary>
    /// Gets or sets the connection string for the SQLite database.
    /// </summary>
    public string ConnectionString { get; set; } = null!;

    /// <summary>
    /// Gets or sets the SQLite journal mode (e.g., WAL, DELETE, TRUNCATE, PERSIST, MEMORY, OFF).
    /// WAL (Write-Ahead Logging) is recommended for better concurrency.
    /// Default is "WAL".
    /// </summary>
    public string? JournalMode { get; set; } = "WAL";

    /// <summary>
    /// Gets or sets the SQLite page size in bytes.
    /// Common values are 1024, 2048, 4096, 8192, 16384, 32768, or 65536.
    /// Default is 4096. Set to null to use SQLite's default.
    /// </summary>
    public int? PageSize { get; set; } = 4096;

    /// <summary>
    /// Gets or sets the SQLite synchronous mode (e.g., FULL, NORMAL, OFF).
    /// NORMAL provides good balance between safety and performance.
    /// Default is "NORMAL".
    /// </summary>
    public string? Synchronous { get; set; } = "NORMAL";

    /// <summary>
    /// Validates the options.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException(
                "SqliteDatabaseOptions must specify a ConnectionString.");
        }

        // Validate page size if specified
        if (PageSize.HasValue)
        {
            var validPageSizes = new[] { 512, 1024, 2048, 4096, 8192, 16384, 32768, 65536 };
            if (Array.IndexOf(validPageSizes, PageSize.Value) == -1)
            {
                throw new InvalidOperationException(
                    $"PageSize must be one of: {string.Join(", ", validPageSizes)}");
            }
        }
    }
}
