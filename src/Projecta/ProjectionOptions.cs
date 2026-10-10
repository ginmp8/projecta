using Projecta.Execution;
using Projecta.Objects;

namespace Projecta.Mapping;

public sealed record ProjectionOptions
{
    public int CacheCapacity { get; init; } = 256;
    public ObjectReadOptions Source { get; init; } = new();
    public ObjectReadOptions Output { get; init; } = new();
    public EvaluationLimits Evaluation { get; init; } = new();
}
