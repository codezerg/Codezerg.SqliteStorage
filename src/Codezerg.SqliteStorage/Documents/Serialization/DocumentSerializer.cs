using System.Text.Json;
using System.Text.Json.Serialization;

namespace Codezerg.SqliteStorage.Documents.Serialization;

/// <summary>
/// Provides JSON serialization for documents.
/// </summary>
internal static class DocumentSerializer
{
    private static readonly JsonSerializerOptions _defaultOptions;

    static DocumentSerializer()
    {
        _defaultOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            Converters =
            {
                new DocumentIdJsonConverter(),
                new JsonStringEnumConverter()
            }
        };
    }

    /// <summary>
    /// Gets the default JSON serializer options.
    /// </summary>
    public static JsonSerializerOptions DefaultOptions => _defaultOptions;

    /// <summary>
    /// Serializes a document to JSON string.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="document">The document to serialize.</param>
    /// <param name="options">Optional serializer options.</param>
    /// <returns>The JSON string representation.</returns>
    public static string Serialize<T>(T document, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Serialize(document, options ?? _defaultOptions);
    }

    /// <summary>
    /// Deserializes a JSON string to a document.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="json">The JSON string.</param>
    /// <param name="options">Optional serializer options.</param>
    /// <returns>The deserialized document.</returns>
    public static T? Deserialize<T>(string json, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        return JsonSerializer.Deserialize<T>(json, options ?? _defaultOptions);
    }

    /// <summary>
    /// Deserializes a JSON string to a document, throwing if null.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="json">The JSON string.</param>
    /// <param name="options">Optional serializer options.</param>
    /// <returns>The deserialized document.</returns>
    /// <exception cref="JsonException">Thrown when deserialization results in null.</exception>
    public static T DeserializeNonNull<T>(string json, JsonSerializerOptions? options = null)
    {
        var result = Deserialize<T>(json, options);
        if (result == null)
            throw new JsonException("Deserialization resulted in null.");
        return result;
    }
}
