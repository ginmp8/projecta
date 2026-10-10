using System;
using System.Threading;
using Projecta.Json;
using Projecta.Objects;

namespace Projecta.Documents;

public static class JsonMergePatch
{
    public static JToken Apply(JToken document, JToken patch, DocumentOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new(); options.Validate();
        TokenValidation.Validate(document, options.Data, cancellationToken); TokenValidation.Validate(patch, options.Data, cancellationToken);
        JToken Merge(JToken target, JToken changes, int depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (depth > options.Data.MaxDepth) throw new DocumentTransformException("merge-depth");
            if (changes is not JObject properties) return changes.DeepClone();
            var result = target is JObject original ? (JObject)original.DeepClone() : new JObject();
            foreach (var pair in properties.Properties)
            {
                if (pair.Value.Type == JTokenType.Null) result.Remove(pair.Key);
                else result.Set(pair.Key, Merge(result.Properties.TryGetValue(pair.Key, out var previous) ? previous : JValue.CreateNull(), pair.Value, depth + 1));
            }
            return result;
        }
        JToken result = Merge(document, patch, 1);
        TokenValidation.Validate(result, options.Data, cancellationToken); return result;
    }
}
