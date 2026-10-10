using System;
using System.Buffers;
using System.Text.Json;
using System.Threading;
using Projecta.Json;
using Projecta.Objects;

namespace Projecta.SystemTextJson;

/// <summary>Writes the native DOM directly to UTF-8. No intermediate JsonNode tree or JSON string is built.</summary>
public static class NativeJsonWriter
{
    public static byte[] ToUtf8Bytes(JToken value, ObjectReadOptions? limits = null, CancellationToken cancellationToken = default)
    {
        limits ??= new ObjectReadOptions();
        TokenValidation.Validate(value, limits, cancellationToken);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { MaxDepth = limits.MaxDepth }))
        { WriteValidated(writer, value, cancellationToken); writer.Flush(); }
        return buffer.WrittenSpan.ToArray();
    }

    public static void Write(Utf8JsonWriter writer, JToken value, ObjectReadOptions? limits = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        TokenValidation.Validate(value, limits, cancellationToken);
        WriteValidated(writer, value, cancellationToken);
    }

    private static void WriteValidated(Utf8JsonWriter writer, JToken value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (value.Type)
        {
            case JTokenType.Object:
                writer.WriteStartObject();
                foreach (var pair in ((JObject)value).Properties)
                { writer.WritePropertyName(pair.Key); WriteValidated(writer, pair.Value, cancellationToken); }
                writer.WriteEndObject(); break;
            case JTokenType.Array:
                writer.WriteStartArray();
                foreach (JToken child in ((JArray)value).ChildrenTokens) WriteValidated(writer, child, cancellationToken);
                writer.WriteEndArray(); break;
            case JTokenType.Null: writer.WriteNullValue(); break;
            case JTokenType.String: writer.WriteStringValue((string)value); break;
            case JTokenType.Boolean: writer.WriteBooleanValue((bool)value); break;
            case JTokenType.Integer: writer.WriteNumberValue((long)value); break;
            case JTokenType.Float:
                writer.WriteRawValue(((JValue)value).GetNumberText());
                break;
            default: throw new ArgumentException("Only defined JSON data can be written.", nameof(value));
        }
    }
}
