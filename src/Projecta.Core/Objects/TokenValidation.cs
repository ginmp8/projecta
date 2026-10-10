using System;
using System.Collections.Generic;
using System.Threading;
using Projecta.Json;

namespace Projecta.Objects;

/// <summary>Bounded traversal of a native JSON data graph; detects cycles before cloning/serialization.</summary>
public static class TokenValidation
{
    public static void Validate(JToken token, ObjectReadOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        options ??= new ObjectReadOptions(); options.Validate();
        int nodes = 0; long characters = 0;
        var active = new HashSet<JToken>(ReferenceEqualityComparer.Instance);
        void Visit(JToken item, int depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++nodes > options.MaxNodes || depth > options.MaxDepth) throw new ObjectReadException("JSON graph limit exceeded.");
            if (!active.Add(item)) throw new ObjectReadException("Circular JSON graph.");
            try
            {
                if (item is JObject o) foreach (var p in o.Properties) { characters += p.Key.Length; Visit(p.Value, depth + 1); }
                else if (item is JArray a) foreach (var v in a.ChildrenTokens) Visit(v, depth + 1);
                else if (item.Type == JTokenType.String) characters += ((string)item).Length;
                else if (item.Type == JTokenType.Function) throw new ObjectReadException("Function is not JSON data.");
                if (characters > options.MaxCharacters) throw new ObjectReadException("JSON graph character limit exceeded.");
            }
            finally { active.Remove(item); }
        }
        Visit(token, 1);
    }
}
