using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Projecta.Json;

namespace Projecta.Mapping;

public sealed record ContractViolation(string JsonPointer, string Keyword, string? ExpectedType = null, string? ActualType = null);

public interface IProjectionContract
{
    IReadOnlyList<ContractViolation> Validate(JToken value, CancellationToken cancellationToken = default);
}

/// <summary>An optional contract compiler. Implementations must reject unsupported assertions instead of silently ignoring them.</summary>
public interface IProjectionContractProvider
{
    IProjectionContract Compile(JsonElement schema);
}

public sealed class ProjectionContractException : Exception
{
    public IReadOnlyList<ContractViolation> Violations { get; }
    public ProjectionContractException(IEnumerable<ContractViolation> violations)
        : base("Data does not satisfy the projection contract.")
    { Violations = new ReadOnlyCollection<ContractViolation>(violations.ToArray()); }
}
