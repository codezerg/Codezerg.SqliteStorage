using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Codezerg.SqliteStorage;

/// <summary>
/// Represents a collection of documents with type-safe operations.
/// </summary>
/// <typeparam name="T">The document type.</typeparam>
public interface IDocumentCollection<T> where T : class
{
    /// <summary>
    /// Gets the collection name.
    /// </summary>
    string CollectionName { get; }

    /// <summary>
    /// Inserts a single document into the collection.
    /// </summary>
    /// <param name="document">The document to insert.</param>
    Task InsertOneAsync(T document);

    /// <summary>
    /// Inserts multiple documents into the collection.
    /// </summary>
    /// <param name="documents">The documents to insert.</param>
    Task InsertManyAsync(IEnumerable<T> documents);

    /// <summary>
    /// Finds a document by its ID.
    /// </summary>
    /// <param name="id">The document ID.</param>
    /// <returns>The document if found, null otherwise.</returns>
    Task<T?> FindByIdAsync(DocumentId id);

    /// <summary>
    /// Finds a single document matching the filter.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <returns>The first matching document, or null if not found.</returns>
    Task<T?> FindOneAsync(Expression<Func<T, bool>> filter);

    /// <summary>
    /// Finds all documents matching the filter.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <returns>A list of matching documents.</returns>
    Task<List<T>> FindAsync(Expression<Func<T, bool>> filter);

    /// <summary>
    /// Finds all documents in the collection.
    /// </summary>
    /// <returns>A list of all documents.</returns>
    Task<List<T>> FindAllAsync();

    /// <summary>
    /// Finds documents with pagination support.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <param name="skip">Number of documents to skip.</param>
    /// <param name="limit">Maximum number of documents to return.</param>
    /// <returns>A list of matching documents.</returns>
    Task<List<T>> FindAsync(Expression<Func<T, bool>> filter, int skip, int limit);

    /// <summary>
    /// Counts documents matching the filter.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <returns>The count of matching documents.</returns>
    Task<long> CountAsync(Expression<Func<T, bool>> filter);

    /// <summary>
    /// Counts all documents in the collection.
    /// </summary>
    /// <returns>The total count of documents.</returns>
    Task<long> CountAllAsync();

    /// <summary>
    /// Updates a single document by its ID.
    /// </summary>
    /// <param name="id">The document ID.</param>
    /// <param name="document">The updated document.</param>
    /// <returns>True if the document was updated, false if not found.</returns>
    Task<bool> UpdateByIdAsync(DocumentId id, T document);

    /// <summary>
    /// Updates a single document matching the filter.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <param name="document">The updated document.</param>
    /// <returns>True if a document was updated, false otherwise.</returns>
    Task<bool> UpdateOneAsync(Expression<Func<T, bool>> filter, T document);

    /// <summary>
    /// Updates multiple documents matching the filter.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <param name="updateAction">An action to update each matching document.</param>
    /// <returns>The number of documents updated.</returns>
    Task<long> UpdateManyAsync(Expression<Func<T, bool>> filter, Action<T> updateAction);

    /// <summary>
    /// Deletes a single document by its ID.
    /// </summary>
    /// <param name="id">The document ID.</param>
    /// <returns>True if the document was deleted, false if not found.</returns>
    Task<bool> DeleteByIdAsync(DocumentId id);

    /// <summary>
    /// Deletes a single document matching the filter.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <returns>True if a document was deleted, false otherwise.</returns>
    Task<bool> DeleteOneAsync(Expression<Func<T, bool>> filter);

    /// <summary>
    /// Deletes all documents matching the filter.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <returns>The number of documents deleted.</returns>
    Task<long> DeleteManyAsync(Expression<Func<T, bool>> filter);

    /// <summary>
    /// Checks if any document matches the filter.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <returns>True if any document matches, false otherwise.</returns>
    Task<bool> AnyAsync(Expression<Func<T, bool>> filter);

    /// <summary>
    /// Creates an index on the specified field.
    /// </summary>
    /// <param name="fieldExpression">The field expression.</param>
    /// <param name="unique">Whether the index should be unique.</param>
    Task CreateIndexAsync<TField>(Expression<Func<T, TField>> fieldExpression, bool unique = false);

    /// <summary>
    /// Drops an index on the specified field.
    /// </summary>
    /// <param name="fieldExpression">The field expression.</param>
    Task DropIndexAsync<TField>(Expression<Func<T, TField>> fieldExpression);
}
