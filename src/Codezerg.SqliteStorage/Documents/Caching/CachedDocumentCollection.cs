using Codezerg.SqliteStorage.Documents.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Codezerg.SqliteStorage.Documents.Caching;

internal class CachedDocumentCollection<T> : IDocumentCollection<T> where T : class
{
    private readonly IDocumentCollection<T> _inner;
    private readonly ConcurrentDictionary<DocumentId, string> _cache = new();
    private int _isLoaded = 0;

    public string CollectionName => _inner.CollectionName;

    public CachedDocumentCollection(IDocumentCollection<T> inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    private async Task EnsureLoadedAsync()
    {
        if (Interlocked.CompareExchange(ref _isLoaded, 1, 0) == 0)
        {
            var allDocuments = await _inner.FindAllAsync();
            foreach (var doc in allDocuments)
            {
                var id = GetDocumentId(doc);
                _cache[id] = DocumentSerializer.Serialize(doc);
            }
        }
    }

    public async Task InsertOneAsync(T document)
    {
        await EnsureLoadedAsync();
        await _inner.InsertOneAsync(document);
        var id = GetDocumentId(document);
        _cache[id] = DocumentSerializer.Serialize(document);
    }

    public async Task InsertManyAsync(IEnumerable<T> documents)
    {
        await EnsureLoadedAsync();
        await _inner.InsertManyAsync(documents);
        foreach (var doc in documents)
        {
            var id = GetDocumentId(doc);
            _cache[id] = DocumentSerializer.Serialize(doc);
        }
    }

    public async Task<T?> FindByIdAsync(DocumentId id)
    {
        await EnsureLoadedAsync();
        return _cache.TryGetValue(id, out var json) ? DocumentSerializer.Deserialize<T>(json) : null;
    }

    public async Task<T?> FindOneAsync(Expression<Func<T, bool>> filter)
    {
        await EnsureLoadedAsync();
        var compiled = filter.Compile();
        foreach (var json in _cache.Values)
        {
            var doc = DocumentSerializer.Deserialize<T>(json);
            if (doc != null && compiled(doc))
            {
                return doc;
            }
        }
        return null;
    }

    public async Task<List<T>> FindAsync(Expression<Func<T, bool>> filter)
    {
        await EnsureLoadedAsync();
        var compiled = filter.Compile();
        var results = new List<T>();
        foreach (var json in _cache.Values)
        {
            var doc = DocumentSerializer.Deserialize<T>(json);
            if (doc != null && compiled(doc))
            {
                results.Add(doc);
            }
        }
        return results;
    }

    public async Task<List<T>> FindAllAsync()
    {
        await EnsureLoadedAsync();
        var results = new List<T>();
        foreach (var json in _cache.Values)
        {
            var doc = DocumentSerializer.Deserialize<T>(json);
            if (doc != null)
            {
                results.Add(doc);
            }
        }
        return results;
    }

    public async Task<List<T>> FindAsync(Expression<Func<T, bool>> filter, int skip, int limit)
    {
        await EnsureLoadedAsync();
        var compiled = filter.Compile();
        var results = new List<T>();
        var skipped = 0;
        var taken = 0;

        foreach (var json in _cache.Values)
        {
            if (taken >= limit) break;

            var doc = DocumentSerializer.Deserialize<T>(json);
            if (doc != null && compiled(doc))
            {
                if (skipped < skip)
                {
                    skipped++;
                }
                else
                {
                    results.Add(doc);
                    taken++;
                }
            }
        }
        return results;
    }

    public async Task<long> CountAsync(Expression<Func<T, bool>> filter)
    {
        await EnsureLoadedAsync();
        var compiled = filter.Compile();
        long count = 0;
        foreach (var json in _cache.Values)
        {
            var doc = DocumentSerializer.Deserialize<T>(json);
            if (doc != null && compiled(doc))
            {
                count++;
            }
        }
        return count;
    }

    public async Task<long> CountAllAsync()
    {
        await EnsureLoadedAsync();
        return _cache.Count;
    }

    public async Task<bool> UpdateByIdAsync(DocumentId id, T document)
    {
        await EnsureLoadedAsync();
        var updated = await _inner.UpdateByIdAsync(id, document);
        if (updated)
        {
            _cache[id] = DocumentSerializer.Serialize(document);
        }
        return updated;
    }

    public async Task<bool> UpdateOneAsync(Expression<Func<T, bool>> filter, T document)
    {
        await EnsureLoadedAsync();
        var updated = await _inner.UpdateOneAsync(filter, document);
        if (updated)
        {
            var id = GetDocumentId(document);
            _cache[id] = DocumentSerializer.Serialize(document);
        }
        return updated;
    }

    public async Task<long> UpdateManyAsync(Expression<Func<T, bool>> filter, Action<T> updateAction)
    {
        await EnsureLoadedAsync();

        // Update in inner collection first
        var count = await _inner.UpdateManyAsync(filter, updateAction);

        // Find matching IDs that need to be reloaded
        var compiled = filter.Compile();
        var matchingIds = new List<DocumentId>();
        foreach (var kvp in _cache)
        {
            var doc = DocumentSerializer.Deserialize<T>(kvp.Value);
            if (doc != null && compiled(doc))
            {
                matchingIds.Add(kvp.Key);
            }
        }

        // Reload from inner collection to update cache
        foreach (var id in matchingIds)
        {
            var updated = await _inner.FindByIdAsync(id);
            if (updated != null)
            {
                _cache[id] = DocumentSerializer.Serialize(updated);
            }
        }

        return count;
    }

    public async Task<bool> DeleteByIdAsync(DocumentId id)
    {
        await EnsureLoadedAsync();
        var deleted = await _inner.DeleteByIdAsync(id);
        if (deleted)
        {
            _cache.TryRemove(id, out _);
        }
        return deleted;
    }

    public async Task<bool> DeleteOneAsync(Expression<Func<T, bool>> filter)
    {
        await EnsureLoadedAsync();
        var deleted = await _inner.DeleteOneAsync(filter);
        if (deleted)
        {
            // Find and remove from cache
            var compiled = filter.Compile();
            foreach (var kvp in _cache)
            {
                var doc = DocumentSerializer.Deserialize<T>(kvp.Value);
                if (doc != null && compiled(doc))
                {
                    _cache.TryRemove(kvp.Key, out _);
                    break;
                }
            }
        }
        return deleted;
    }

    public async Task<long> DeleteManyAsync(Expression<Func<T, bool>> filter)
    {
        await EnsureLoadedAsync();

        // Find matching IDs to remove
        var compiled = filter.Compile();
        var matchingIds = new List<DocumentId>();
        foreach (var kvp in _cache)
        {
            var doc = DocumentSerializer.Deserialize<T>(kvp.Value);
            if (doc != null && compiled(doc))
            {
                matchingIds.Add(kvp.Key);
            }
        }

        // Delete from inner collection
        var count = await _inner.DeleteManyAsync(filter);

        // Remove from cache
        foreach (var id in matchingIds)
        {
            _cache.TryRemove(id, out _);
        }

        return count;
    }

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> filter)
    {
        await EnsureLoadedAsync();
        var compiled = filter.Compile();
        foreach (var json in _cache.Values)
        {
            var doc = DocumentSerializer.Deserialize<T>(json);
            if (doc != null && compiled(doc))
            {
                return true;
            }
        }
        return false;
    }

    public Task CreateIndexAsync<TField>(Expression<Func<T, TField>> fieldExpression, bool unique = false)
        => _inner.CreateIndexAsync(fieldExpression, unique);

    public Task DropIndexAsync<TField>(Expression<Func<T, TField>> fieldExpression)
        => _inner.DropIndexAsync(fieldExpression);

    private static DocumentId GetDocumentId(T document)
    {
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty != null && idProperty.PropertyType == typeof(DocumentId))
        {
            return (DocumentId)(idProperty.GetValue(document) ?? DocumentId.Empty);
        }

        var idField = typeof(T).GetField("_id");
        if (idField != null && idField.FieldType == typeof(DocumentId))
        {
            return (DocumentId)(idField.GetValue(document) ?? DocumentId.Empty);
        }

        return DocumentId.Empty;
    }
}
