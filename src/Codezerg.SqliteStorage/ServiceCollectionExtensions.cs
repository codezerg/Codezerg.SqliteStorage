using Codezerg.SqliteStorage.Configuration;
using Codezerg.SqliteStorage.Documents;
using Codezerg.SqliteStorage.Documents.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace Codezerg.SqliteStorage;

/// <summary>
/// Extension methods for configuring storage services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds shared SQLite database options to the service collection.
    /// All storage components (DocumentDatabase, ContentStore, Repository) will use these settings.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="SqliteDatabaseOptionsBuilder"/>.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    public static IServiceCollection AddSqliteDatabase(
        this IServiceCollection services,
        Action<SqliteDatabaseOptionsBuilder> configureOptions)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (configureOptions == null)
            throw new ArgumentNullException(nameof(configureOptions));

        var builder = new SqliteDatabaseOptionsBuilder();
        configureOptions(builder);
        var options = builder.Build();

        // Register options
        services.Configure<SqliteDatabaseOptions>(opt =>
        {
            opt.ConnectionString = options.ConnectionString;
            opt.ProviderName = options.ProviderName;
            opt.JournalMode = options.JournalMode;
            opt.PageSize = options.PageSize;
            opt.Synchronous = options.Synchronous;
        });

        services.TryAddSingleton<ISqliteConnectionProvider, SqliteConnectionProvider>();

        return services;
    }

    /// <summary>
    /// Adds document database services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="DocumentDatabaseOptions"/>.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    public static IServiceCollection AddDocumentDatabase(
        this IServiceCollection services,
        Action<DocumentDatabaseOptionsBuilder> configureOptions)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (configureOptions == null)
            throw new ArgumentNullException(nameof(configureOptions));

        var builder = new DocumentDatabaseOptionsBuilder();
        configureOptions(builder);
        var options = builder.Build();

        // Register options
        services.Configure<DocumentDatabaseOptions>(opt =>
        {
            opt.Storage = options.Storage;
            opt.ConnectionString = options.ConnectionString;
            opt.ProviderName = options.ProviderName;
            opt.JournalMode = options.JournalMode;
            opt.PageSize = options.PageSize;
            opt.Synchronous = options.Synchronous;
            opt.UseJsonB = options.UseJsonB;
            opt.CachedCollectionPredicates = options.CachedCollectionPredicates;
        });

        // Register database as singleton
        services.TryAddSingleton<IDocumentDatabase, SqliteDocumentDatabase>();

        return services;
    }
}
