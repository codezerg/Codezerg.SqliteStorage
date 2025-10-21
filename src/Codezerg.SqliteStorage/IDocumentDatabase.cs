using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Codezerg.SqliteStorage;

/// <summary>
/// Represents a document database using SQLite as storage.
/// </summary>
public interface IDocumentDatabase : IDisposable
{
    /// <summary>
    /// Gets a collection by name.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="name">The collection name.</param>
    /// <returns>A collection instance.</returns>
    Task<IDocumentCollection<T>> GetCollectionAsync<T>(string name) where T : class;

    /// <summary>
    /// Creates a new collection if it doesn't exist.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="name">The collection name.</param>
    Task CreateCollectionAsync<T>(string name) where T : class;

    /// <summary>
    /// Drops a collection from the database.
    /// </summary>
    /// <param name="name">The collection name.</param>
    Task DropCollectionAsync(string name);

    /// <summary>
    /// Lists all collection names in the database.
    /// </summary>
    /// <returns>A list of collection names.</returns>
    Task<List<string>> ListCollectionNamesAsync();

    /// <summary>
    /// Gets the database name.
    /// </summary>
    string DatabaseName { get; }

    /// <summary>
    /// Gets the connection string used by this database.
    /// </summary>
    string ConnectionString { get; }
}