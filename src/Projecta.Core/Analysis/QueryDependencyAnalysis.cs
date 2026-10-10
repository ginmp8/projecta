using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Projecta.Impl;

namespace Projecta.Analysis;

/// <summary>A conservative, immutable analysis. Unknown/dynamic constructs require the full input.</summary>
public sealed class QueryDependencyAnalysis
{
    public bool CanSelectRootMembers { get; }
    public IReadOnlyList<string> RootMembers { get; }
    public string? FallbackReason { get; }

    private QueryDependencyAnalysis(bool safe, IEnumerable<string> members, string? reason)
    {
        CanSelectRootMembers = safe;
        RootMembers = new ReadOnlyCollection<string>(members.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        FallbackReason = reason;
    }

    public static QueryDependencyAnalysis Analyze(ProjectaQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var roots = new HashSet<string>(StringComparer.Ordinal);
        string? reason = null;
        bool Visit(Node n, int depth)
        {
            if (depth > 128) { reason = "analysis-depth"; return false; }
            if (n.group is not null || n.predicate is { Count: > 0 } || n.stages is { Count: > 0 } || n.tuple)
            { reason = "context-changing-expression"; return false; }
            switch (n)
            {
                case NameNode name: roots.Add(name.value); return true;
                case PathRuntimeNode path when path.steps.Count > 0 && path.steps.All(s => s is NameNode &&
                    s.group is null && s.predicate is not { Count: > 0 } && s.stages is not { Count: > 0 } && !s.tuple):
                    roots.Add(((NameNode)path.steps[0]).value); return true;
                case StringNode or NumberIntNode or NumberDoubleNode or BoolNode or NullNode: return true;
                case VariableNode variable when variable.value.Length > 0 && variable.value != "$": return true;
                case UnaryMinusNode unary: return Visit(unary.expression, depth + 1);
                case BinaryNode binary: return Visit(binary.lhs, depth + 1) && Visit(binary.rhs, depth + 1);
                case ConditionNode condition:
                    return Visit(condition.condition, depth + 1) && Visit(condition.then, depth + 1) &&
                        (condition.@else is null || Visit(condition.@else, depth + 1));
                case ArrayNode array: return array.expressions.All(e => Visit(e, depth + 1));
                case GroupNode group when group.lhsObject.All(p => p.Item1 is StringNode):
                    return group.lhsObject.All(p => Visit(p.Item2, depth + 1));
                case BlockNode block when block.expressions.Count == 1: return Visit(block.expressions[0], depth + 1);
                default: reason = "dynamic-or-context-dependent-expression"; return false;
            }
        }
        bool safe = Visit(query.GetAst(), 0);
        return new QueryDependencyAnalysis(safe, safe ? roots : Array.Empty<string>(), reason);
    }
}
