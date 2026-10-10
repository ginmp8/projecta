using System;
using System.Collections.Generic;
using Projecta.Objects;

namespace Projecta.Documents;

public sealed record DocumentOptions
{
    public int MaxOperations { get; init; } = 256;
    public int MaxDefinitionCharacters { get; init; } = 1_000_000;
    public ObjectReadOptions Data { get; init; } = new();
    internal void Validate()
    {
        if (MaxOperations < 1 || MaxDefinitionCharacters < 1 || Data is null) throw new ArgumentOutOfRangeException(nameof(DocumentOptions));
    }
}
public sealed class DocumentTransformException : Exception
{
    public string Code { get; }
    public int? OperationIndex { get; }
    public DocumentTransformException(string code, int? operationIndex = null) : base("Document operation failed: " + code)
    { Code = code; OperationIndex = operationIndex; }
}
