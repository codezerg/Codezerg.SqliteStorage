using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Codezerg.SqliteStorage.Documents.Serialization;

/// <summary>
/// JSON converter for DocumentId type.
/// </summary>
internal class DocumentIdJsonConverter : JsonConverter<DocumentId>
{
    public override DocumentId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return DocumentId.Empty;

        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (string.IsNullOrEmpty(str))
                return DocumentId.Empty;

            if (DocumentId.TryParse(str, out var id))
                return id;

            throw new JsonException($"Invalid DocumentId format: {str}");
        }

        throw new JsonException($"Unexpected token type for DocumentId: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, DocumentId value, JsonSerializerOptions options)
    {
        if (value == DocumentId.Empty)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
