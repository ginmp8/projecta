using System;
using Projecta.Json;
using Projecta.Mapping;
using Projecta.Objects;
using N = Newtonsoft.Json.Linq;

namespace Projecta.NewtonsoftJson;

/// <summary>Native token conversion through the shared bounded reader; no JSON-text round trip.</summary>
public sealed class NewtonsoftSourceAdapter : IObjectSourceAdapter
{
    public bool CanRead(object source) => source is N.JToken;
    public JToken Read(object source, ObjectReadContext context)
    {
        if (source is N.JObject obj)
        {
            var result = new JObject();
            foreach (var p in obj.Properties()) result.Add(p.Name, context.Read(p.Value));
            return result;
        }
        if (source is N.JArray array)
        {
            var result = new JArray();
            foreach (var value in array) result.Add(context.Read(value));
            return result;
        }
        if (source is N.JValue scalar)
        {
            if (scalar.Type is N.JTokenType.Raw or N.JTokenType.Undefined or N.JTokenType.Comment or N.JTokenType.None)
                throw new ObjectReadException("Raw, undefined and comment tokens are not projection input data.");
            if (scalar.Value is Uri uri) return context.Read(uri.ToString());
            if (scalar.Value is byte[] bytes) return context.Read(Convert.ToBase64String(bytes));
            return context.Read(scalar.Value);
        }
        throw new ObjectReadException("Unsupported Newtonsoft token kind.");
    }
}

public sealed class NewtonsoftTargetMaterializer : IProjectionTargetMaterializer
{
    public bool CanMaterialize(Type targetType) => typeof(N.JToken).IsAssignableFrom(targetType);
    public object Materialize(JToken value, Type targetType)
    {
        N.JToken token = value.ToNewtonsoft();
        return targetType.IsInstanceOfType(token) ? token :
            throw new ProjectionMaterializationException("Unexpected Newtonsoft token shape.");
    }
}
