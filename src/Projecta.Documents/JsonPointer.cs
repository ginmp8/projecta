using System;
using System.Globalization;
using System.Text;
using Projecta.Json;
using Projecta.Mapping;
using Projecta.Objects;

namespace Projecta.Documents;

/// <summary>An immutable JSON Pointer in string form, not a URI fragment. Public reads return detached data.</summary>
public sealed class JsonPointer
{
    public string Value { get; }
    internal string[] Segments { get; }
    private JsonPointer(string value, string[] segments) { Value = value; Segments = segments; }
    public static JsonPointer Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > 4096 || value.Length > 0 && value[0] != '/') throw new ProjectionConfigurationException("Invalid JSON Pointer.");
        if (value.Length == 0) return new(value, Array.Empty<string>());
        string[] segments = value[1..].Split('/');
        if (segments.Length > 64) throw new ProjectionConfigurationException("JSON Pointer depth exceeded.");
        for (int n = 0; n < segments.Length; n++)
        {
            string segment = segments[n]; var decoded = new StringBuilder();
            for (int i = 0; i < segment.Length; i++)
            {
                if (segment[i] != '~') decoded.Append(segment[i]);
                else if (++i < segment.Length && segment[i] is '0' or '1') decoded.Append(segment[i] == '0' ? '~' : '/');
                else throw new ProjectionConfigurationException("Invalid JSON Pointer escape.");
            }
            segments[n] = decoded.ToString();
        }
        return new(value, segments);
    }
    public JToken Resolve(JToken document)
    {
        if (!TryResolve(document, out var value)) throw new DocumentTransformException("path-not-found");
        return value!;
    }
    public bool TryResolve(JToken document, out JToken? value)
    {
        TokenValidation.Validate(document);
        if (!Find(document, out value)) return false;
        value = value!.DeepClone(); return true;
    }
    internal bool Find(JToken document, out JToken? value)
    {
        JToken current = document;
        foreach (string segment in Segments)
        {
            if (current is JObject o && o.Properties.TryGetValue(segment, out var child)) current = child;
            else if (current is JArray a && Index(segment, a.Count, false, out int index)) current = a.ChildrenTokens[index];
            else { value = null; return false; }
        }
        value = current; return true;
    }
    internal static bool Index(string segment, int count, bool append, out int index)
    {
        index = -1;
        if (append && segment == "-") { index = count; return true; }
        if (segment.Length == 0 || segment.Length > 1 && segment[0] == '0') return false;
        foreach (char c in segment) if (c is < '0' or > '9') return false;
        return int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out index) && index >= 0 && (append ? index <= count : index < count);
    }
    internal bool IsStrictAncestorOf(JsonPointer other)
    {
        if (Segments.Length >= other.Segments.Length) return false;
        for (int i = 0; i < Segments.Length; i++) if (Segments[i] != other.Segments[i]) return false;
        return true;
    }
}
