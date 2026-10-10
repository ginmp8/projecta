using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Projecta.Impl;
using Projecta.Mapping;
using LinqQueryable = System.Linq.Queryable;

namespace Projecta.Queryable;

public sealed class ProjectionTranslationException : Exception
{
    public string Code { get; }
    public string? Field { get; }
    public ProjectionTranslationException(string code, string? field = null) : base("Query projection is outside the supported scalar-select-v1 profile.")
    { Code = code; Field = field; }
}

/// <summary>Builds scalar Select expressions only. It never runs a query or falls back to client evaluation.</summary>
public static class QueryProjection
{
    public static IQueryable<TTarget> ProjectTo<TSource, TTarget>(this IQueryable<TSource> source, ProjectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(source);
        return LinqQueryable.Select(source, Build<TSource, TTarget>(definition));
    }
    public static Expression<Func<TSource, TTarget>> Build<TSource, TTarget>(ProjectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.InputSchema.HasValue || definition.OutputSchema.HasValue || definition.TargetType is not null)
            throw new ProjectionTranslationException("runtime-contract-not-translatable");
        if (definition.Version < 1 || string.IsNullOrWhiteSpace(definition.Id)) throw new ProjectionTranslationException("invalid-definition");
        Dictionary<string, Node> fields = ReadFields(definition);
        Type target = typeof(TTarget);
        if (target.IsAbstract || target.IsInterface || target.ContainsGenericParameters) throw new ProjectionTranslationException("invalid-target");
        var properties = target.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.GetIndexParameters().Length == 0 && p.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition != JsonIgnoreCondition.Always).ToArray();
        var names = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
        foreach (var p in properties)
            if (!names.TryAdd(JsonName(p), p)) throw new ProjectionTranslationException("ambiguous-target-member");
        foreach (string field in fields.Keys) if (!names.ContainsKey(field)) throw new ProjectionTranslationException("unknown-target-member", field);
        foreach (var property in properties)
            if (property.IsDefined(typeof(RequiredMemberAttribute)) && !fields.ContainsKey(JsonName(property)))
                throw new ProjectionTranslationException("missing-required-member", JsonName(property));
        var parameter = Expression.Parameter(typeof(TSource), "source");
        var translated = new Dictionary<string, Expression>(StringComparer.Ordinal);
        foreach (var field in fields)
            translated.Add(field.Key, Translate(field.Value, names[field.Key].PropertyType, parameter, field.Key));

        var constructors = target.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        var marked = constructors.Where(c => c.IsDefined(typeof(JsonConstructorAttribute))).ToArray();
        if (marked.Length > 1) throw new ProjectionTranslationException("ambiguous-constructor");
        var candidates = marked.Length == 1 ? marked : constructors;
        var selected = candidates.Where(c => c.GetParameters().All(p => ResolveParameter(p, names, translated) is not null))
            .OrderByDescending(c => c.GetParameters().Length).ToArray();
        NewExpression construction;
        var consumed = new HashSet<string>(StringComparer.Ordinal);
        if (selected.Length == 0)
        {
            if (!target.IsValueType || marked.Length != 0) throw new ProjectionTranslationException("no-supported-constructor");
            construction = Expression.New(target);
        }
        else
        {
            var constructor = selected[0];
            if (selected.Length > 1 && selected[1].GetParameters().Length == constructor.GetParameters().Length)
                throw new ProjectionTranslationException("ambiguous-constructor");
            var arguments = new List<Expression>();
            foreach (var p in constructor.GetParameters())
            {
                string field = ResolveParameter(p, names, translated)!; consumed.Add(field);
                arguments.Add(ConvertLossless(translated[field], p.ParameterType, field));
            }
            construction = Expression.New(constructor, arguments);
        }
        var bindings = new List<MemberBinding>();
        foreach (var field in translated)
        {
            if (consumed.Contains(field.Key)) continue;
            var property = names[field.Key];
            if (property.SetMethod?.IsPublic != true) throw new ProjectionTranslationException("member-not-writable", field.Key);
            bindings.Add(Expression.Bind(property, field.Value));
        }
        Expression body = bindings.Count == 0 ? construction : Expression.MemberInit(construction, bindings);
        return Expression.Lambda<Func<TSource, TTarget>>(body, parameter);
    }
    private static string? ResolveParameter(ParameterInfo parameter, Dictionary<string, PropertyInfo> names, Dictionary<string, Expression> values)
    {
        foreach (var pair in names)
            if (string.Equals(pair.Value.Name, parameter.Name, StringComparison.OrdinalIgnoreCase) && values.ContainsKey(pair.Key) && pair.Value.PropertyType == parameter.ParameterType)
                return pair.Key;
        return null;
    }
    private static Dictionary<string, Node> ReadFields(ProjectionDefinition definition)
    {
        bool hasFields = definition.Fields is { Count: > 0 }, hasExpression = !string.IsNullOrWhiteSpace(definition.Expression);
        if (hasFields == hasExpression) throw new ProjectionTranslationException("choose-fields-or-expression");
        var result = new Dictionary<string, Node>(StringComparer.Ordinal);
        if (hasFields)
        {
            if (definition.Fields!.Count > 256) throw new ProjectionTranslationException("field-limit");
            foreach (var pair in definition.Fields) result.Add(pair.Key, Parse(pair.Value, pair.Key));
        }
        else
        {
            Node root = Unwrap(Parse(definition.Expression!, null)); EnsurePlain(root, null);
            if (root is not GroupNode group || group.lhsObject.Count > 256) throw new ProjectionTranslationException("object-projection-required");
            foreach (var pair in group.lhsObject)
            {
                if (pair.Item1 is not StringNode key || !result.TryAdd(key.value, pair.Item2)) throw new ProjectionTranslationException("literal-unique-keys-required");
            }
        }
        if (result.Count == 0) throw new ProjectionTranslationException("empty-projection");
        return result;
    }
    private static Node Parse(string text, string? field)
    {
        try { return new ProjectaQuery(text).GetAst(); }
        catch (JsonataException) { throw new ProjectionTranslationException("invalid-expression", field); }
    }
    private static Node Unwrap(Node node) => node is BlockNode block && block.expressions.Count == 1 ? Unwrap(block.expressions[0]) : node;
    private static void EnsurePlain(Node n, string? field)
    {
        if (n.group is not null || n.stages is { Count: > 0 } || n.predicate is { Count: > 0 } || n.keepArray || n.tuple)
            throw new ProjectionTranslationException("context-operation-not-supported", field);
    }
    private static Expression Translate(Node node, Type target, ParameterExpression source, string field)
    {
        node = Unwrap(node); EnsurePlain(node, field);
        if (node is NullNode)
        {
            if (target.IsValueType && Nullable.GetUnderlyingType(target) is null) throw new ProjectionTranslationException("null-to-nonnullable", field);
            return Expression.Constant(null, target);
        }
        if (node is StringNode text) return ConvertLossless(Expression.Constant(text.value), target, field);
        if (node is BoolNode boolean) return ConvertLossless(Expression.Constant(boolean.value), target, field);
        if (node is NumberIntNode integer) return NumericConstant(integer.value, target, field);
        if (node is NumberDoubleNode number) return NumericConstant(number.value, target, field);
        if (node is UnaryMinusNode unary && Unwrap(unary.expression) is NumberIntNode i) return NumericConstant(checked(-i.value), target, field);
        if (node is UnaryMinusNode u && Unwrap(u.expression) is NumberDoubleNode d) return NumericConstant(-d.value, target, field);
        IReadOnlyList<Node> steps = node switch
        { NameNode => new[] { node }, PathRuntimeNode path => path.steps, _ => throw new ProjectionTranslationException("unsupported-expression", field) };
        Expression current = source; Expression? guard = null;
        foreach (var step in steps)
        {
            EnsurePlain(step, field);
            if (step is not NameNode name) throw new ProjectionTranslationException("static-member-path-required", field);
            Type owner = Nullable.GetUnderlyingType(current.Type) ?? current.Type;
            if (!current.Type.IsValueType)
            {
                Expression notNull = Expression.NotEqual(current, Expression.Constant(null, current.Type));
                guard = guard is null ? notNull : Expression.AndAlso(guard, notNull);
            }
            else if (Nullable.GetUnderlyingType(current.Type) is not null)
            {
                Expression hasValue = Expression.Property(current, "HasValue");
                guard = guard is null ? hasValue : Expression.AndAlso(guard, hasValue);
                current = Expression.Property(current, "Value");
            }
            var matches = owner.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetMethod?.IsPublic == true && p.GetIndexParameters().Length == 0 &&
                p.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition != JsonIgnoreCondition.Always && JsonName(p) == name.value).ToArray();
            if (matches.Length != 1) throw new ProjectionTranslationException("unknown-or-ambiguous-source-member", field);
            current = Expression.Property(current, matches[0]);
        }
        if (!Scalar(current.Type) || !Scalar(target)) throw new ProjectionTranslationException("scalar-members-only", field);
        Expression value = ConvertLossless(current, target, field);
        return guard is null ? value : Expression.Condition(guard, value, Expression.Default(target));
    }
    private static string JsonName(PropertyInfo property) => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
    private static bool Scalar(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) || type == typeof(Guid) || type == typeof(DateOnly) || type == typeof(TimeOnly) || type == typeof(TimeSpan);
    }
    private static Expression ConvertLossless(Expression value, Type target, string field)
    {
        if (value.Type == target) return value;
        Type sourceType = Nullable.GetUnderlyingType(value.Type) ?? value.Type;
        Type targetType = Nullable.GetUnderlyingType(target) ?? target;
        if (Nullable.GetUnderlyingType(value.Type) is not null && Nullable.GetUnderlyingType(target) is null)
            throw new ProjectionTranslationException("nullable-to-nonnullable", field);
        bool allowed = sourceType == targetType || (sourceType == typeof(byte) || sourceType == typeof(short) || sourceType == typeof(int)) &&
            (targetType == typeof(long) || targetType == typeof(decimal) || targetType == typeof(double)) || sourceType == typeof(long) && targetType == typeof(decimal) ||
            sourceType == typeof(float) && targetType == typeof(double);
        if (!allowed) throw new ProjectionTranslationException("conversion-not-lossless", field);
        return Expression.Convert(value, target);
    }
    private static Expression NumericConstant(object number, Type target, string field)
    {
        Type core = Nullable.GetUnderlyingType(target) ?? target;
        if (core != typeof(byte) && core != typeof(short) && core != typeof(int) && core != typeof(long) && core != typeof(decimal) && core != typeof(double) && core != typeof(float))
            throw new ProjectionTranslationException("numeric-target-required", field);
        try
        {
            if (number is double d && core != typeof(double) && core != typeof(float) && core != typeof(decimal) && Math.Truncate(d) != d)
                throw new ProjectionTranslationException("fractional-integer", field);
            object value = Convert.ChangeType(number, core, CultureInfo.InvariantCulture);
            Expression constant = Expression.Constant(value, core);
            return core == target ? constant : Expression.Convert(constant, target);
        }
        catch (Exception ex) when (ex is OverflowException or InvalidCastException or FormatException)
        { throw new ProjectionTranslationException("constant-out-of-range", field); }
    }
}
