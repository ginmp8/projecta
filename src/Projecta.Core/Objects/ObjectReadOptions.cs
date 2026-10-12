using System;
using System.Reflection;
using System.Collections.Generic;

namespace Projecta.Objects;

public sealed record ObjectReadOptions
{
    public int MaxDepth { get; init; } = 64;
    public int MaxNodes { get; init; } = 100_000;
    public int MaxCharacters { get; init; } = 2_000_000;
    public bool UseJsonPropertyNames { get; init; } = true;
    /// <summary>Optional root-member exposure boundary across POCOs, dictionaries and JSON DOMs.
    /// An empty set exposes no root members. Callbacks in a custom adapter can still read their own input.</summary>
    public IReadOnlyCollection<string>? AllowedRootMembers { get; init; }

    public Func<Type, PropertyInfo, bool>? IncludeProperty { get; init; }

    internal void Validate()
    {
        if (MaxDepth is < 1 or > 256 || MaxNodes < 1 || MaxCharacters < 1)
            throw new ArgumentOutOfRangeException(nameof(ObjectReadOptions));
        if (AllowedRootMembers is not null)
        {
            if (AllowedRootMembers.Count > 1024 || System.Linq.Enumerable.Any(AllowedRootMembers,
                s => string.IsNullOrEmpty(s) || s.Length > 256))
                throw new ArgumentOutOfRangeException(nameof(AllowedRootMembers));
        }
    }
}

public sealed class ObjectReadException : Exception
{
    public ObjectReadException(string message, Exception? inner = null) : base(message, inner) { }
}
