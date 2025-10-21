using System;

namespace Codezerg.SqliteStorage.Documents.Configuration;

/// <summary>
/// Builder for configuring <see cref="DocumentDatabaseOptions"/> with a fluent API.
/// </summary>
public class DocumentDatabaseOptionsBuilder
{
    private readonly DocumentDatabaseOptions _options = new();

    /// <summary>
    /// Configures the ADO.NET provider name for the database connection.
    /// </summary>
    /// <param name="providerName">The ADO.NET provider name (e.g., "Microsoft.Data.Sqlite", "System.Data.SQLite").</param>
    /// <returns>The builder for fluent chaining.</returns>
    public DocumentDatabaseOptionsBuilder UseProviderName(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name cannot be null or empty.", nameof(providerName));

        _options.ProviderName = providerName;
        return this;
    }

    /// <summary>
    /// Configures the database to use a specific connection string.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public DocumentDatabaseOptionsBuilder UseConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));

        _options.ConnectionString = connectionString;
        return this;
    }

    /// <summary>
    /// Configures the database to use JSONB (binary JSON) storage format.
    /// JSONB provides significant performance improvements (20-60% faster operations) with 5-10% storage savings.
    /// </summary>
    /// <param name="useJsonB">True to enable JSONB storage, false for JSON text storage.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public DocumentDatabaseOptionsBuilder UseJsonB(bool useJsonB = true)
    {
        _options.UseJsonB = useJsonB;
        return this;
    }

    /// <summary>
    /// Configures the SQLite journal mode.
    /// WAL (Write-Ahead Logging) is recommended for better concurrency.
    /// </summary>
    /// <param name="journalMode">The journal mode (e.g., WAL, DELETE, TRUNCATE, PERSIST, MEMORY, OFF).</param>
    /// <returns>The builder for fluent chaining.</returns>
    public DocumentDatabaseOptionsBuilder UseJournalMode(string? journalMode)
    {
        _options.JournalMode = journalMode;
        return this;
    }

    /// <summary>
    /// Configures the SQLite page size in bytes.
    /// Common values are 1024, 2048, 4096, 8192, 16384, 32768, or 65536.
    /// </summary>
    /// <param name="pageSize">The page size in bytes.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public DocumentDatabaseOptionsBuilder UsePageSize(int? pageSize)
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
    public DocumentDatabaseOptionsBuilder UseSynchronous(string? synchronous)
    {
        _options.Synchronous = synchronous;
        return this;
    }


    /// <summary>
    /// Marks a collection to be cached in memory for faster access.
    /// </summary>
    /// <param name="collectionName">The name of the collection to cache.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public DocumentDatabaseOptionsBuilder CacheCollection(string collectionName)
    {
        _options.CachedCollectionPredicates.Add(name =>
            string.Equals(name, collectionName, StringComparison.OrdinalIgnoreCase));
        return this;
    }

    /// <summary>
    /// Configures which collections should be cached using a predicate function.
    /// </summary>
    /// <param name="predicate">A function that determines if a collection should be cached based on its name.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public DocumentDatabaseOptionsBuilder CacheCollections(Func<string, bool> predicate)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        _options.CachedCollectionPredicates.Add(predicate);
        return this;
    }


    /// <summary>
    /// Builds the configured options.
    /// </summary>
    /// <returns>The configured <see cref="DocumentDatabaseOptions"/>.</returns>
    internal DocumentDatabaseOptions Build()
    {
        _options.Validate();
        return _options;
    }
}
