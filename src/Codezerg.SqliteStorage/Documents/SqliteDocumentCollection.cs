using Codezerg.SqliteStorage.Documents.Serialization;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Codezerg.SqliteStorage.Documents;

/// <summary>
/// SQLite-backed document collection implementation.
/// </summary>
/// <typeparam name="T">The document type.</typeparam>
internal class SqliteDocumentCollection<T> : IDocumentCollection<T> where T : class
{
    private readonly SqliteDocumentDatabase _database;
    private readonly string _collectionName;
    private readonly ILogger _logger;
    private readonly bool _useJsonB;
    private long? _collectionId;

    public string CollectionName => _collectionName;

    public SqliteDocumentCollection(
        SqliteDocumentDatabase database,
        string collectionName,
        ILogger logger,
        bool useJsonB)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _collectionName = collectionName ?? throw new ArgumentNullException(nameof(collectionName));
        _logger = logger ?? NullLogger.Instance;
        _useJsonB = useJsonB;
    }

    private async Task<long> GetCollectionIdAsync()
    {
        if (_collectionId.HasValue)
            return _collectionId.Value;

        using (var connection = _database.CreateConnection())
        {
            var sql = "SELECT id FROM collections WHERE name = @Name LIMIT 1;";
            var id = await connection.QuerySingleOrDefaultAsync<long?>(sql, new { Name = _collectionName });

            if (!id.HasValue)
                throw new InvalidOperationException($"Collection '{_collectionName}' does not exist.");

            _collectionId = id.Value;
            return _collectionId.Value;
        }
    }

    public async Task InsertOneAsync(T document)
    {
        await InsertManyAsync(new[] { document });
    }

    public async Task InsertManyAsync(IEnumerable<T> documents)
    {
        if (documents == null)
            throw new ArgumentNullException(nameof(documents));

        var documentList = documents.ToList();
        if (documentList.Count == 0)
            return;

        var collectionId = await GetCollectionIdAsync();

        // Prepare all documents and parameters first
        var insertParams = new List<(DocumentId id, object parameters)>();
        var now = DateTime.UtcNow;

        foreach (var document in documentList)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var id = GetDocumentId(document);
            // Check if ID is empty or default (ToString returns empty string for both)
            if (id == DocumentId.Empty || string.IsNullOrEmpty(id.ToString()))
            {
                id = DocumentId.NewId();
                SetDocumentId(document, id);
            }

            SetTimestamps(document, isNew: true);

            var json = DocumentSerializer.Serialize(document);

            insertParams.Add((id, new
            {
                CollectionId = collectionId,
                DocumentId = id.ToString(),
                Data = json,
                CreatedAt = now.ToString("O"),
                UpdatedAt = now.ToString("O")
            }));
        }

        // Execute all inserts within a single connection and transaction
        var dataParam = _useJsonB ? "jsonb(@Data)" : "@Data";
        var sql = $@"
            INSERT INTO documents (collection_id, document_id, data, created_at, updated_at, version)
            VALUES (@CollectionId, @DocumentId, {dataParam}, @CreatedAt, @UpdatedAt, 1);";

        using (var connection = _database.CreateConnection())
        {
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    foreach (var (id, parameters) in insertParams)
                    {
                        await connection.ExecuteAsync(sql, parameters, transaction);
                        _logger.LogDebug("Inserted document {Id} into {Collection}", id, _collectionName);
                    }

                    transaction.Commit();
                }
                catch (DbException ex) when (IsConstraintViolation(ex))
                {
                    transaction.Rollback();
                    // Try to determine which document caused the issue
                    var failedId = insertParams.FirstOrDefault().id;
                    throw new InvalidOperationException($"Duplicate key '{failedId}' in collection '{_collectionName}'.", ex);
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        _logger.LogDebug("Inserted {Count} documents into {Collection}", documentList.Count, _collectionName);
    }

    public async Task<T?> FindByIdAsync(DocumentId id)
    {
        var collectionId = await GetCollectionIdAsync();

        var dataSelect = _useJsonB ? "json(data)" : "data";
        var sql = $"SELECT {dataSelect} FROM documents WHERE collection_id = @CollectionId AND document_id = @DocumentId LIMIT 1;";

        using (var connection = _database.CreateConnection())
        {
            var json = await connection.QuerySingleOrDefaultAsync<string>(sql, new
            {
                CollectionId = collectionId,
                DocumentId = id.ToString()
            });

            if (string.IsNullOrEmpty(json))
                return null;

            return DocumentSerializer.Deserialize<T>(json!);
        }
    }

    public async Task<T?> FindOneAsync(Expression<Func<T, bool>> filter)
    {
        var collectionId = await GetCollectionIdAsync();
        var (whereClause, parameters) = QueryTranslator.Translate(filter);

        var dataSelect = _useJsonB ? "json(data)" : "data";
        var sql = $"SELECT {dataSelect} FROM documents WHERE collection_id = @CollectionId AND {whereClause} LIMIT 1;";

        using (var connection = _database.CreateConnection())
        {
            var dynamicParams = CreateDynamicParameters(parameters);
            dynamicParams.Add("CollectionId", collectionId);
            var json = await connection.QuerySingleOrDefaultAsync<string>(sql, dynamicParams);

            if (string.IsNullOrEmpty(json))
                return null;

            return DocumentSerializer.Deserialize<T>(json!);
        }
    }

    public async Task<List<T>> FindAsync(Expression<Func<T, bool>> filter)
    {
        var collectionId = await GetCollectionIdAsync();
        var (whereClause, parameters) = QueryTranslator.Translate(filter);

        var dataSelect = _useJsonB ? "json(data)" : "data";
        var sql = $"SELECT {dataSelect} FROM documents WHERE collection_id = @CollectionId AND {whereClause};";

        using (var connection = _database.CreateConnection())
        {
            var dynamicParams = CreateDynamicParameters(parameters);
            dynamicParams.Add("CollectionId", collectionId);
            var jsonResults = await connection.QueryAsync<string>(sql, dynamicParams);

            var results = new List<T>();
            foreach (var json in jsonResults)
            {
                var doc = DocumentSerializer.Deserialize<T>(json);
                if (doc != null)
                    results.Add(doc);
            }

            return results;
        }
    }

    public async Task<List<T>> FindAllAsync()
    {
        var collectionId = await GetCollectionIdAsync();

        var dataSelect = _useJsonB ? "json(data)" : "data";
        var sql = $"SELECT {dataSelect} FROM documents WHERE collection_id = @CollectionId;";

        using (var connection = _database.CreateConnection())
        {
            var jsonResults = await connection.QueryAsync<string>(sql, new { CollectionId = collectionId });

            var results = new List<T>();
            foreach (var json in jsonResults)
            {
                var doc = DocumentSerializer.Deserialize<T>(json);
                if (doc != null)
                    results.Add(doc);
            }

            return results;
        }
    }

    public async Task<List<T>> FindAsync(Expression<Func<T, bool>> filter, int skip, int limit)
    {
        var collectionId = await GetCollectionIdAsync();
        var (whereClause, parameters) = QueryTranslator.Translate(filter);

        var dataSelect = _useJsonB ? "json(data)" : "data";
        var sql = $"SELECT {dataSelect} FROM documents WHERE collection_id = @CollectionId AND {whereClause} LIMIT @Limit OFFSET @Skip;";

        using (var connection = _database.CreateConnection())
        {
            var dynamicParams = CreateDynamicParameters(parameters);
            dynamicParams.Add("CollectionId", collectionId);
            dynamicParams.Add("Limit", limit);
            dynamicParams.Add("Skip", skip);

            var jsonResults = await connection.QueryAsync<string>(sql, dynamicParams);

            var results = new List<T>();
            foreach (var json in jsonResults)
            {
                var doc = DocumentSerializer.Deserialize<T>(json);
                if (doc != null)
                    results.Add(doc);
            }

            return results;
        }
    }

    public async Task<long> CountAsync(Expression<Func<T, bool>> filter)
    {
        var collectionId = await GetCollectionIdAsync();
        var (whereClause, parameters) = QueryTranslator.Translate(filter);

        var sql = $"SELECT COUNT(*) FROM documents WHERE collection_id = @CollectionId AND {whereClause};";

        using (var connection = _database.CreateConnection())
        {
            var dynamicParams = CreateDynamicParameters(parameters);
            dynamicParams.Add("CollectionId", collectionId);
            return await connection.ExecuteScalarAsync<long>(sql, dynamicParams);
        }
    }

    public async Task<long> CountAllAsync()
    {
        var collectionId = await GetCollectionIdAsync();

        var sql = "SELECT COUNT(*) FROM documents WHERE collection_id = @CollectionId;";

        using (var connection = _database.CreateConnection())
        {
            return await connection.ExecuteScalarAsync<long>(sql, new { CollectionId = collectionId });
        }
    }

    public async Task<bool> UpdateByIdAsync(DocumentId id, T document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        var collectionId = await GetCollectionIdAsync();

        SetDocumentId(document, id);
        SetTimestamps(document, isNew: false);

        var json = DocumentSerializer.Serialize(document);
        var now = DateTime.UtcNow;

        var dataParam = _useJsonB ? "jsonb(@Data)" : "@Data";
        var sql = $@"
            UPDATE documents
            SET data = {dataParam}, updated_at = @UpdatedAt, version = version + 1
            WHERE collection_id = @CollectionId AND document_id = @DocumentId;";

        using (var connection = _database.CreateConnection())
        {
            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                CollectionId = collectionId,
                DocumentId = id.ToString(),
                Data = json,
                UpdatedAt = now.ToString("O")
            });

            _logger.LogDebug("Updated document {Id} in {Collection}", id, _collectionName);

            return rowsAffected > 0;
        }
    }

    public async Task<bool> UpdateOneAsync(Expression<Func<T, bool>> filter, T document)
    {
        var existing = await FindOneAsync(filter);
        if (existing == null)
            return false;

        var id = GetDocumentId(existing);
        return await UpdateByIdAsync(id, document);
    }

    public async Task<long> UpdateManyAsync(Expression<Func<T, bool>> filter, Action<T> updateAction)
    {
        var documents = await FindAsync(filter);
        long updatedCount = 0;

        foreach (var doc in documents)
        {
            updateAction(doc);
            var id = GetDocumentId(doc);
            if (await UpdateByIdAsync(id, doc))
                updatedCount++;
        }

        return updatedCount;
    }

    public async Task<bool> DeleteByIdAsync(DocumentId id)
    {
        var collectionId = await GetCollectionIdAsync();

        var sql = "DELETE FROM documents WHERE collection_id = @CollectionId AND document_id = @DocumentId;";

        using (var connection = _database.CreateConnection())
        {
            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                CollectionId = collectionId,
                DocumentId = id.ToString()
            });

            _logger.LogDebug("Deleted document {Id} from {Collection}", id, _collectionName);

            return rowsAffected > 0;
        }
    }

    public async Task<bool> DeleteOneAsync(Expression<Func<T, bool>> filter)
    {
        var doc = await FindOneAsync(filter);
        if (doc == null)
            return false;

        var id = GetDocumentId(doc);
        return await DeleteByIdAsync(id);
    }

    public async Task<long> DeleteManyAsync(Expression<Func<T, bool>> filter)
    {
        var collectionId = await GetCollectionIdAsync();
        var (whereClause, parameters) = QueryTranslator.Translate(filter);

        var sql = $"DELETE FROM documents WHERE collection_id = @CollectionId AND {whereClause};";

        using (var connection = _database.CreateConnection())
        {
            var dynamicParams = CreateDynamicParameters(parameters);
            dynamicParams.Add("CollectionId", collectionId);
            var rowsAffected = await connection.ExecuteAsync(sql, dynamicParams);

            _logger.LogDebug("Deleted {Count} documents from {Collection}", rowsAffected, _collectionName);

            return rowsAffected;
        }
    }

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> filter)
    {
        var count = await CountAsync(filter);
        return count > 0;
    }

    public async Task CreateIndexAsync<TField>(Expression<Func<T, TField>> fieldExpression, bool unique = false)
    {
        var collectionId = await GetCollectionIdAsync();
        var fieldName = GetFieldName(fieldExpression);
        var indexName = $"idx_{_collectionName}_{fieldName}";
        var uniqueKeyword = unique ? "UNIQUE" : "";

        using (var connection = _database.CreateConnection())
        {
            // Insert into indexes metadata table
            var insertIndexSql = @"
                INSERT OR IGNORE INTO indexes (collection_id, name, fields, unique_index, sparse)
                VALUES (@CollectionId, @Name, @Fields, @Unique, 0);";

            await connection.ExecuteAsync(insertIndexSql, new
            {
                CollectionId = collectionId,
                Name = indexName,
                Fields = $"[{fieldName}]",
                Unique = unique ? 1 : 0
            });

            // Create actual SQLite index on documents table
            var createIndexSql = $@"
                CREATE {uniqueKeyword} INDEX IF NOT EXISTS {indexName}
                ON documents (json_extract(data, '$.{fieldName}'))
                WHERE collection_id = {collectionId};";

            await connection.ExecuteAsync(createIndexSql);

            _logger.LogInformation("Created index {IndexName} on {Collection}", indexName, _collectionName);
        }
    }

    public async Task DropIndexAsync<TField>(Expression<Func<T, TField>> fieldExpression)
    {
        var collectionId = await GetCollectionIdAsync();
        var fieldName = GetFieldName(fieldExpression);
        var indexName = $"idx_{_collectionName}_{fieldName}";

        using (var connection = _database.CreateConnection())
        {
            // Drop the actual SQLite index
            var dropIndexSql = $"DROP INDEX IF EXISTS {indexName};";
            await connection.ExecuteAsync(dropIndexSql);

            // Delete from indexes metadata table
            var deleteIndexSql = @"
                DELETE FROM indexes
                WHERE collection_id = @CollectionId AND name = @Name;";

            await connection.ExecuteAsync(deleteIndexSql, new
            {
                CollectionId = collectionId,
                Name = indexName
            });

            _logger.LogInformation("Dropped index {IndexName} on {Collection}", indexName, _collectionName);
        }
    }

    private static DocumentId GetDocumentId(T document)
    {
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty != null && idProperty.PropertyType == typeof(DocumentId))
        {
            return (DocumentId)(idProperty.GetValue(document) ?? DocumentId.Empty);
        }

        // Check for _id field
        var idField = typeof(T).GetField("_id");
        if (idField != null && idField.FieldType == typeof(DocumentId))
        {
            return (DocumentId)(idField.GetValue(document) ?? DocumentId.Empty);
        }

        return DocumentId.Empty;
    }

    private static void SetDocumentId(T document, DocumentId id)
    {
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty != null && idProperty.PropertyType == typeof(DocumentId) && idProperty.CanWrite)
        {
            idProperty.SetValue(document, id);
            return;
        }

        var idField = typeof(T).GetField("_id");
        if (idField != null && idField.FieldType == typeof(DocumentId))
        {
            idField.SetValue(document, id);
        }
    }

    private static void SetTimestamps(T document, bool isNew)
    {
        var now = DateTime.UtcNow;

        if (isNew)
        {
            var createdAtProperty = typeof(T).GetProperty("CreatedAt");
            if (createdAtProperty != null && createdAtProperty.PropertyType == typeof(DateTime) && createdAtProperty.CanWrite)
            {
                createdAtProperty.SetValue(document, now);
            }
        }

        var updatedAtProperty = typeof(T).GetProperty("UpdatedAt");
        if (updatedAtProperty != null && updatedAtProperty.PropertyType == typeof(DateTime) && updatedAtProperty.CanWrite)
        {
            updatedAtProperty.SetValue(document, now);
        }
    }

    private static string GetFieldName<TField>(Expression<Func<T, TField>> fieldExpression)
    {
        if (fieldExpression.Body is MemberExpression member)
        {
            return ToCamelCase(member.Member.Name);
        }

        throw new NotSupportedException($"Invalid field expression: {fieldExpression}");
    }

    private static string ToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str) || char.IsLower(str[0]))
            return str;

        return char.ToLowerInvariant(str[0]) + str.Substring(1);
    }

    private static DynamicParameters CreateDynamicParameters(IReadOnlyList<object?> parameters)
    {
        var dynamicParams = new DynamicParameters();
        for (int i = 0; i < parameters.Count; i++)
        {
            dynamicParams.Add($"p{i}", parameters[i]);
        }
        return dynamicParams;
    }

    /// <summary>
    /// Checks if a database exception represents a constraint violation.
    /// This checks for common SQLite constraint error codes and messages.
    /// </summary>
    private static bool IsConstraintViolation(DbException ex)
    {
        // Check for SQLite CONSTRAINT error code (19)
        // Most SQLite providers expose this via ErrorCode property
        if (ex.ErrorCode == 19)
            return true;

        // Also check the message for constraint-related keywords as fallback
        var message = ex.Message?.ToLowerInvariant() ?? string.Empty;
        return message.Contains("constraint") ||
               message.Contains("unique") ||
               message.Contains("duplicate");
    }
}
