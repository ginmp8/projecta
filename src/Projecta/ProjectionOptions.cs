using Projecta.Execution;
using Projecta.Objects;

namespace Projecta.Mapping;

public sealed record ProjectionOptions
{
    public int CacheCapacity { get; init; } = 256;
    public int MaxConcurrentCompilations { get; init; } = 4;
    public bool EnableSelectiveReading { get; init; }
    public IProjectionContractProvider? ContractProvider { get; init; }
    public ProjectionFunctionCatalog? Functions { get; init; }
    public ObjectReadOptions Bindings { get; init; } = new() { MaxNodes = 10_000, MaxCharacters = 250_000 };
    public ObjectReadOptions Source { get; init; } = new();
    public ObjectReadOptions Output { get; init; } = new();
    public EvaluationLimits Evaluation { get; init; } = new();
}
