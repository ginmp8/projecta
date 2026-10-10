using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Projecta.Json;
using Projecta.Objects;

namespace Projecta.Mapping;

public delegate JToken ProjectionFunctionCallback(IReadOnlyList<JToken> arguments, CancellationToken cancellationToken);

/// <summary>Trusted host code; purity is a host assertion, not a sandbox guarantee.</summary>
public sealed record ProjectionFunction(string Name, int MinimumArguments, int MaximumArguments,
    ProjectionFunctionCallback Callback, string? Signature = null, bool IsPure = true);

/// <summary>Immutable allowlisted function catalog. It does not load code, assemblies or services from configuration.</summary>
public sealed class ProjectionFunctionCatalog
{
    private readonly IReadOnlyDictionary<string, ProjectionFunction> _functions;
    public static ProjectionFunctionCatalog Empty { get; } = new(Array.Empty<ProjectionFunction>());
    public ProjectionFunctionCatalog(IEnumerable<ProjectionFunction> functions)
    {
        ArgumentNullException.ThrowIfNull(functions);
        var map = new Dictionary<string, ProjectionFunction>(StringComparer.Ordinal);
        foreach (var function in functions)
        {
            ArgumentNullException.ThrowIfNull(function);
            ProjectionExecutionContext.ValidateName(function.Name);
            ArgumentNullException.ThrowIfNull(function.Callback);
            if (function.MinimumArguments < 0 || function.MaximumArguments < function.MinimumArguments || function.MaximumArguments > 64)
                throw new ArgumentOutOfRangeException(nameof(functions));
            if (map.Count >= 128 || !map.TryAdd(function.Name, function))
                throw new ProjectionConfigurationException("Duplicate function or catalog limit exceeded.");
            // Validate the language signature once, not at first production use.
            new EvaluationEnvironment().BindHostFunction(function.Name, _ => JValue.CreateNull(),
                function.MinimumArguments, function.MaximumArguments, function.Signature);
        }
        _functions = new ReadOnlyDictionary<string, ProjectionFunction>(map);
    }
    internal ProjectionFunction Resolve(string name) => _functions.TryGetValue(name, out var f) ? f :
        throw new ProjectionConfigurationException("Requested function is not registered.");
}

/// <summary>A detached snapshot of variables and allowed function names, safe to reuse across independent calls.</summary>
public sealed class ProjectionExecutionContext
{
    private readonly JObject _variables;
    private readonly string[] _functions;
    public bool AllowImpureFunctions { get; }
    public static ProjectionExecutionContext Empty { get; } = new();

    public ProjectionExecutionContext(IReadOnlyDictionary<string, object?>? variables = null,
        IEnumerable<string>? allowedFunctions = null, ObjectReadOptions? bindingLimits = null,
        bool allowImpureFunctions = false)
    {
        var snapshot = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (variables is not null)
            foreach (var pair in variables)
            {
                ValidateName(pair.Key);
                if (snapshot.Count >= 128 || !snapshot.TryAdd(pair.Key, pair.Value))
                    throw new ProjectionConfigurationException("Duplicate variable or binding limit exceeded.");
            }
        _variables = (JObject)new ObjectReader(bindingLimits).Read(snapshot);
        var functions = new HashSet<string>(StringComparer.Ordinal);
        if (allowedFunctions is not null)
            foreach (string name in allowedFunctions)
            {
                ValidateName(name);
                if (snapshot.ContainsKey(name)) throw new ProjectionConfigurationException("A variable cannot shadow an allowed function.");
                if (functions.Count >= 128) throw new ProjectionConfigurationException("Function allowlist limit exceeded.");
                functions.Add(name);
            }
        _functions = functions.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        AllowImpureFunctions = allowImpureFunctions;
    }

    internal static void ValidateName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 128 ||
            !(char.IsAsciiLetter(name[0]) || name[0] == '_') ||
            name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_') || EvaluationEnvironment.IsBuiltinName(name))
            throw new ProjectionConfigurationException("Names must be unprefixed identifiers and cannot replace built-ins.");
    }

    internal EvaluationEnvironment CreateEnvironment(ProjectionFunctionCatalog catalog, ObjectReadOptions limits,
        CancellationToken cancellationToken)
    {
        var reader = new ObjectReader(limits);
        var environment = new EvaluationEnvironment((JObject)reader.Read(_variables, cancellationToken));
        foreach (string name in _functions)
        {
            ProjectionFunction function = catalog.Resolve(name);
            if (!function.IsPure && !AllowImpureFunctions)
                throw new ProjectionConfigurationException("Impure functions require explicit execution opt-in.");
            environment.BindHostFunction(name, args =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var array = new JArray();
                foreach (JToken arg in args) array.Add(arg);
                var detached = (JArray)reader.Read(array, cancellationToken);
                JToken result = function.Callback(detached.ChildrenTokens, cancellationToken)
                    ?? throw new InvalidOperationException("A host function must return a JSON token.");
                return reader.Read(result, cancellationToken);
            }, function.MinimumArguments, function.MaximumArguments, function.Signature);
        }
        return environment;
    }
}
