using System;
using Projecta.Objects;

namespace Projecta.Contracts;

public sealed record JsonSchemaContractOptions
{
    public int MaxSchemaCharacters { get; init; } = 1_000_000;
    public int MaxSchemaNodes { get; init; } = 4096;
    public int MaxDepth { get; init; } = 64;
    public int MaxSteps { get; init; } = 200_000;
    public int MaxErrors { get; init; } = 32;
    public int MaxPatternLength { get; init; } = 512;
    public TimeSpan RegexTimeout { get; init; } = TimeSpan.FromMilliseconds(100);
    public ObjectReadOptions DataLimits { get; init; } = new();

    internal void Validate()
    {
        if (MaxSchemaCharacters < 1 || MaxSchemaNodes < 1 || MaxDepth is < 1 or > 128 ||
            MaxSteps < 1 || MaxErrors < 1 || MaxPatternLength < 1 || RegexTimeout <= TimeSpan.Zero ||
            RegexTimeout > TimeSpan.FromSeconds(1) || DataLimits is null)
            throw new ArgumentOutOfRangeException(nameof(JsonSchemaContractOptions));
    }
}
