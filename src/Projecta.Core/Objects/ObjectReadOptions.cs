using System;
using System.Reflection;

namespace Projecta.Objects;

public sealed record ObjectReadOptions
{
    public int MaxDepth { get; init; } = 64;
    public int MaxNodes { get; init; } = 100_000;
    public int MaxCharacters { get; init; } = 2_000_000;
    public bool UseJsonPropertyNames { get; init; } = true;
    public Func<Type, PropertyInfo, bool>? IncludeProperty { get; init; }

    internal void Validate()
    {
        if (MaxDepth is < 1 or > 256 || MaxNodes < 1 || MaxCharacters < 1)
            throw new ArgumentOutOfRangeException(nameof(ObjectReadOptions));
    }
}

public sealed class ObjectReadException : Exception
{
    public ObjectReadException(string message, Exception? inner = null) : base(message, inner) { }
}
