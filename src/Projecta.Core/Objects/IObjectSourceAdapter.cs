using Projecta.Json;

namespace Projecta.Objects;

/// <summary>Explicit source integration. Adapters can be scoped, e.g. to a DbContext.</summary>
public interface IObjectSourceAdapter
{
    bool CanRead(object source);
    JToken Read(object source, ObjectReadContext context);
}
