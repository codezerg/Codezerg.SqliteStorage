using Codezerg.SqliteStorage.Common;
using Codezerg.SqliteStorage.Configuration;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;
using System.Data.Common;

namespace Codezerg.SqliteStorage;

public class SqliteConnectionProvider : ISqliteConnectionProvider
{
    private readonly DbProviderFactory _providerFactory;
    private readonly SqliteDatabaseOptions _options;
    private readonly ILogger<SqliteConnectionProvider>? _logger;

    public SqliteConnectionProvider(IOptions<SqliteDatabaseOptions> options, ILogger<SqliteConnectionProvider>? logger = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger<SqliteConnectionProvider>.Instance;

        _providerFactory = DbProviderFactories.GetFactory(_options.ProviderName);
        if (_providerFactory == null)
            throw new InvalidOperationException($"Failed to get provider factory for provider name '{_options.ProviderName}'.");
    }

    /// <summary>
    /// Creates a new database connection for the current operation (internal use).
    /// </summary>
    public DbConnection CreateConnection()
    {
        var connection = _providerFactory.CreateConnection();
        if (connection == null)
            throw new InvalidOperationException($"Failed to create connection from provider '{_options.ProviderName}'.");

        connection.ConnectionString = _options.ConnectionString;
        connection.Open();

        // Apply pragmas to each connection
        ApplyPragmas(connection);

        return connection;
    }

    private void ApplyPragmas(DbConnection connection)
    {
        // Apply journal mode
        var journalMode = _options.JournalMode;
        if (!string.IsNullOrWhiteSpace(journalMode))
        {
            var journalSql = $"PRAGMA journal_mode = {journalMode};";
            connection.Execute(journalSql);
            _logger?.LogDebug("Applied PRAGMA journal_mode = {JournalMode}", journalMode);
        }

        // Apply page size (must be set before any tables are created)
        var pageSize = _options.PageSize;
        if (pageSize.HasValue)
        {
            var pageSizeSql = $"PRAGMA page_size = {pageSize.Value};";
            connection.Execute(pageSizeSql);
            _logger?.LogDebug("Applied PRAGMA page_size = {PageSize}", pageSize.Value);
        }

        // Apply synchronous mode
        var synchronous = _options.Synchronous;
        if (!string.IsNullOrWhiteSpace(synchronous))
        {
            var syncSql = $"PRAGMA synchronous = {synchronous};";
            connection.Execute(syncSql);
            _logger?.LogDebug("Applied PRAGMA synchronous = {Synchronous}", synchronous);
        }
    }
}
