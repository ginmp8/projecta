using System;
using System.Collections.Generic;
using Projecta.Execution;
using Projecta.Impl;
using Projecta.Json;

namespace Projecta.Eval;

internal sealed class HostFunctionToken : FunctionToken
{
    private readonly Func<IReadOnlyList<JToken>, JToken> _callback;
    private readonly int _minArguments, _maxArguments;

    internal HostFunctionToken(string name, Func<IReadOnlyList<JToken>, JToken> callback,
        int minArguments, int maxArguments, string? signature)
        : base(name, maxArguments, signature is null ? null : new Signature(signature))
    {
        _callback = callback;
        _minArguments = minArguments;
        _maxArguments = maxArguments;
        RequiredArgsCount = minArguments;
    }

    internal override JToken Apply(JsThisArgument jsThis, List<JToken> args)
    {
        EvaluationBudget.CheckpointCurrent();
        if (args.Count < _minArguments || args.Count > _maxArguments)
            throw new JsonataException(JsonataErrorCode.T0410, "Host function argument count is outside its declared contract.");
        JToken result = _callback(args) ?? throw new InvalidOperationException("Host function returned no token.");
        EvaluationBudget.CheckpointCurrent();
        return result;
    }

    public override JToken DeepClone() => this;
}
