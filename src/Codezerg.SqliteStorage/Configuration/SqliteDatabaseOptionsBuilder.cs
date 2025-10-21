using System;

namespace Codezerg.SqliteStorage.Configuration;

/// <summary>
/// Builder for configuring <see cref="SqliteDatabaseOptions"/> with a fluent API.
/// </summary>
public class SqliteDatabaseOptionsBuilder
{
    private readonly SqliteDatabaseOptions _options = new();

    /// <summary>
    /// Configures the database to use a specific connection string.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public SqliteDatabaseOptionsBuilder UseConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));

        _options.ConnectionString = connectionString;
        return this;
    }

    /// <summary>
    /// Configures the ADO.NET provider name for the database connection.
    /// </summary>
    /// <param name="providerName">The ADO.NET provider name (e.g., "Microsoft.Data.Sqlite", "System.Data.SQLite").</param>
    /// <returns>The builder for fluent chaining.</returns>
    public SqliteDatabaseOptionsBuilder UseProviderName(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name cannot be null or empty.", nameof(providerName));

        _options.ProviderName = providerName;
        return this;
    }

    /// <summary>
    /// Configures the SQLite journal mode.
    /// WAL (Write-Ahead Logging) is recommended for better concurrency.
    /// </summary>
    /// <param name="journalMode">The journal mode (e.g., WAL, DELETE, TRUNCATE, PERSIST, MEMORY, OFF).</param>
    /// <returns>The builder for fluent chaining.</returns>
    public SqliteDatabaseOptionsBuilder UseJournalMode(string? journalMode)
    {
        _options.JournalMode = journalMode;
        return this;
    }

    /// <summary>
    /// Configures the database to use WAL (Write-Ahead Logging) mode.
    /// WAL mode offers better concurrency by allowing readers and writers to operate concurrently.
    /// </summary>
    /// <returns>The builder for fluent chaining.</returns>
    public SqliteDatabaseOptionsBuilder UseWalMode()
    {
        _options.JournalMode = "WAL";
        return this;
    }

    /// <summary>
    /// Configures the SQLite page size in bytes.
    /// Common values are 1024, 2048, 4096, 8192, 16384, 32768, or 65536.
    /// </summary>
    /// <param name="pageSize">The page size in bytes.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public SqliteDatabaseOptionsBuilder UsePageSize(int pageSize)
    {
        _options.PageSize = pageSize;
        return this;
    }

    /// <summary>
    /// Configures the SQLite synchronous mode.
    /// NORMAL provides a good balance between safety and performance.
    /// </summary>
    /// <param name="synchronous">The synchronous mode (e.g., FULL, NORMAL, OFF).</param>
    /// <returns>The builder for fluent chaining.</returns>
    public SqliteDatabaseOptionsBuilder UseSynchronous(string? synchronous)
    {
        _options.Synchronous = synchronous;
        return this;
    }

    /// <summary>
    /// Configures the database to use FULL synchronous mode for maximum safety.
    /// This is the safest mode but also the slowest.
    /// </summary>
    /// <returns>The builder for fluent chaining.</returns>
    public SqliteDatabaseOptionsBuilder UseFullSynchronous()
    {
        _options.Synchronous = "FULL";
        return this;
    }

    /// <summary>
    /// Configures the database to use NORMAL synchronous mode.
    /// This provides a good balance between safety and performance.
    /// </summary>
    /// <returns>The builder for fluent chaining.</returns>
    public SqliteDatabaseOptionsBuilder UseNormalSynchronous()
    {
        _options.Synchronous = "NORMAL";
        return this;
    }

    /// <summary>
    /// Builds the configured options.
    /// </summary>
    /// <returns>The configured <see cref="SqliteDatabaseOptions"/>.</returns>
    internal SqliteDatabaseOptions Build()
    {
        _options.Validate();
        return _options;
    }
}
