using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Projecta.Impl;
using Projecta.Execution;

namespace Projecta.SourceGeneration;

[Generator]
public sealed class StaticProjectionGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor SignatureError = new("PJG001", "Unsupported mapper declaration",
        "Static projection requires a non-generic public/internal static partial method with one source parameter in a top-level static partial class", "Projecta", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor ExpressionError = new("PJG002", "Unsupported static expression",
        "Projection expression is outside the static-scalar-v1 profile", "Projecta", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor TargetError = new("PJG003", "Incompatible static mapping",
        "Source and destination members cannot be mapped without an unsupported conversion or missing required member", "Projecta", DiagnosticSeverity.Error, true);
    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
        SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(c => c.AddSource("StaticProjectionAttribute.g.cs", """
            #nullable enable
            namespace Projecta.Generated;
            [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
            internal sealed class StaticProjectionAttribute : global::System.Attribute
            {
                public string Expression { get; }
                public StaticProjectionAttribute(string expression) => Expression = expression;
            }
            """));
        var methods = context.SyntaxProvider.ForAttributeWithMetadataName("Projecta.Generated.StaticProjectionAttribute",
            static (node, _) => node is MethodDeclarationSyntax,
            static (ctx, _) => (Method: (IMethodSymbol)ctx.TargetSymbol, Expression: ctx.Attributes[0].ConstructorArguments[0].Value as string));
        context.RegisterSourceOutput(methods, (output, item) => Generate(output, item.Method, item.Expression));
    }
    private static void Generate(SourceProductionContext output, IMethodSymbol method, string? expression)
    {
        output.CancellationToken.ThrowIfCancellationRequested();
        var owner = method.ContainingType;
        if (!method.IsStatic || !method.IsPartialDefinition || method.IsGenericMethod || method.Parameters.Length != 1 ||
            method.Parameters[0].RefKind != RefKind.None || method.ReturnsByRef || method.ReturnsByRefReadonly ||
            method.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal) || !owner.IsStatic ||
            owner.IsGenericType || owner.ContainingType is not null || method.ReturnType is not INamedTypeSymbol target ||
            target.IsAbstract || target.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        { output.ReportDiagnostic(Diagnostic.Create(SignatureError, method.Locations.FirstOrDefault())); return; }
        try
        {
            if (expression is null) throw new GenerationFailure(ExpressionError);
            Node root;
            try { root = Unwrap(new ProjectaQuery(expression).GetAst()); }
            catch (Exception ex) when (ex is JsonataException or ExecutionLimitException) { throw new GenerationFailure(ExpressionError); }
            Plain(root);
            if (root is not GroupNode group || group.lhsObject.Count is < 1 or > 256) throw new GenerationFailure(ExpressionError);
            var targetProperties = Properties(target).ToArray();
            var fields = new Dictionary<string, (IPropertySymbol Property, Node Node)>(StringComparer.Ordinal);
            foreach (var pair in group.lhsObject)
            {
                if (pair.Item1 is not StringNode key) throw new GenerationFailure(ExpressionError);
                var properties = targetProperties.Where(p => JsonName(p) == key.value).ToArray();
                if (properties.Length != 1 || fields.ContainsKey(key.value)) throw new GenerationFailure(TargetError);
                fields.Add(key.value, (properties[0], pair.Item2));
            }
            if (targetProperties.Any(p => p.IsRequired && !fields.Values.Any(x => SymbolEqualityComparer.Default.Equals(x.Property, p))))
                throw new GenerationFailure(TargetError);
            var values = fields.ToDictionary(p => p.Key, p => EmitValue(p.Value.Node, p.Value.Property.Type, method.Parameters[0]), StringComparer.Ordinal);
            string? FieldFor(IParameterSymbol p) => fields.FirstOrDefault(f => f.Value.Property.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase) &&
                SymbolEqualityComparer.Default.Equals(p.Type, f.Value.Property.Type)).Key;
            var constructors = target.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public).ToArray();
            var marked = constructors.Where(c => HasAttribute(c, "System.Text.Json.Serialization.JsonConstructorAttribute")).ToArray();
            if (marked.Length > 1) throw new GenerationFailure(TargetError);
            var candidates = (marked.Length == 1 ? marked : constructors).Where(c => c.Parameters.All(p => FieldFor(p) is not null))
                .OrderByDescending(c => c.Parameters.Length).ToArray();
            if (candidates.Length == 0 || candidates.Length > 1 && candidates[0].Parameters.Length == candidates[1].Parameters.Length)
                throw new GenerationFailure(TargetError);
            var selected = candidates[0];
            bool constructorSetsRequired = HasAttribute(selected, "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute");
            // Evaluate each mapped value once, including required properties also bound by a constructor.
            var declarations = new List<string>();
            int localIndex = 0;
            foreach (string field in values.Keys.ToArray())
            {
                string local;
                do { local = "__projectaValue" + localIndex++; } while (local == method.Parameters[0].Name);
                declarations.Add("var " + local + " = " + values[field] + ";");
                values[field] = local;
            }
            var used = new HashSet<string>(StringComparer.Ordinal);
            var arguments = new List<string>();
            foreach (var parameter in selected.Parameters)
            { string field = FieldFor(parameter)!; used.Add(field); arguments.Add(Id(parameter.Name) + ": " + values[field]); }
            var assignments = new List<string>();
            foreach (var field in fields)
            {
                if (used.Contains(field.Key) && (!field.Value.Property.IsRequired || constructorSetsRequired)) continue;
                if (field.Value.Property.SetMethod?.DeclaredAccessibility != Accessibility.Public) throw new GenerationFailure(TargetError);
                assignments.Add(Id(field.Value.Property.Name) + " = " + values[field.Key]);
            }
            string creation = "new " + TypeName(target) + "(" + string.Join(", ", arguments) + ")";
            if (assignments.Count > 0) creation += " { " + string.Join(", ", assignments) + " }";
            string ns = owner.ContainingNamespace.IsGlobalNamespace ? "" : "namespace " + owner.ContainingNamespace.ToDisplayString() + " {\n";
            string methodAccess = method.DeclaredAccessibility == Accessibility.Public ? "public" : "internal";
            string classAccess = owner.DeclaredAccessibility == Accessibility.Public ? "public" : "internal";
            string parameterName = Id(method.Parameters[0].Name);
            string guard = method.Parameters[0].Type.IsReferenceType ? "global::System.ArgumentNullException.ThrowIfNull(" + parameterName + ");" : "";
            string source = "#nullable enable\n" + ns + classAccess + " static partial class " + Id(owner.Name) + "\n{\n" +
                "    " + methodAccess + " static partial " + TypeName(target) + " " + Id(method.Name) + "(" + TypeName(method.Parameters[0].Type) + " " + parameterName + ")\n    {\n        " +
                guard + "\n        " + string.Join("\n        ", declarations) + "\n        return " + creation + ";\n    }\n}\n" + (ns.Length > 0 ? "}\n" : "");
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(method.ToDisplayString())))[..16];
            output.AddSource("Projection_" + hash + ".g.cs", source);
        }
        catch (OverflowException) { output.ReportDiagnostic(Diagnostic.Create(TargetError, method.Locations.FirstOrDefault())); }
        catch (GenerationFailure failure) { output.ReportDiagnostic(Diagnostic.Create(failure.Descriptor, method.Locations.FirstOrDefault())); }
    }
    private static string EmitValue(Node node, ITypeSymbol target, IParameterSymbol source)
    {
        node = Unwrap(node); Plain(node);
        if (node is NullNode)
        {
            if (!Nullable(target)) throw new GenerationFailure(TargetError);
            return "default(" + TypeName(target) + ")";
        }
        if (node is StringNode text)
        {
            if (Underlying(target).SpecialType != SpecialType.System_String) throw new GenerationFailure(TargetError);
            return SymbolDisplay.FormatLiteral(text.value, true);
        }
        if (node is BoolNode boolean)
        {
            if (Underlying(target).SpecialType != SpecialType.System_Boolean) throw new GenerationFailure(TargetError);
            return boolean.value ? "true" : "false";
        }
        if (node is NumberIntNode integer) return Number(integer.value, target);
        if (node is NumberDoubleNode number) return Number(number.value, target);
        if (node is UnaryMinusNode unary && Unwrap(unary.expression) is NumberIntNode negative) return Number(checked(-negative.value), target);
        IReadOnlyList<Node> steps = node switch
        { NameNode => new[] { node }, PathRuntimeNode path => path.steps, _ => throw new GenerationFailure(ExpressionError) };
        ITypeSymbol type = source.Type; string access = Id(source.Name); var guards = new List<string>();
        for (int i = 0; i < steps.Count; i++)
        {
            Plain(steps[i]); if (steps[i] is not NameNode name) throw new GenerationFailure(ExpressionError);
            if (i > 0 && type.IsReferenceType) guards.Add(access + " != null");
            if (IsNullableValue(type)) { guards.Add(access + ".HasValue"); access += ".Value"; type = Underlying(type); }
            var properties = Properties(type).Where(p => p.GetMethod?.DeclaredAccessibility == Accessibility.Public && JsonName(p) == name.value).ToArray();
            if (properties.Length != 1) throw new GenerationFailure(TargetError);
            access += "." + Id(properties[0].Name); type = properties[0].Type;
        }
        if (!Scalar(type) || !Scalar(target) || !Convertible(type, target)) throw new GenerationFailure(TargetError);
        if (type.NullableAnnotation == NullableAnnotation.Annotated && target.IsReferenceType && target.NullableAnnotation == NullableAnnotation.NotAnnotated)
            throw new GenerationFailure(TargetError);
        string value = "(" + TypeName(target) + ")(" + access + ")";
        if (guards.Count == 0) return value;
        if (target.IsReferenceType && target.NullableAnnotation != NullableAnnotation.Annotated) throw new GenerationFailure(TargetError);
        return "(" + string.Join(" && ", guards) + " ? " + value + " : default(" + TypeName(target) + "))";
    }
    private static string Number(object number, ITypeSymbol target)
    {
        var kind = Underlying(target).SpecialType;
        try
        {
            string value = kind switch
            {
                SpecialType.System_Int32 when number is long l => checked((int)l).ToString(CultureInfo.InvariantCulture),
                SpecialType.System_Int64 when number is long l => l.ToString(CultureInfo.InvariantCulture) + "L",
                SpecialType.System_Decimal => Convert.ToDecimal(number, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) + "M",
                SpecialType.System_Double => Convert.ToDouble(number, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture) + "D",
                _ => throw new GenerationFailure(TargetError)
            };
            return "(" + TypeName(target) + ")(" + value + ")";
        }
        catch (OverflowException) { throw new GenerationFailure(TargetError); }
    }
    private static bool Convertible(ITypeSymbol source, ITypeSymbol target)
    {
        if (IsNullableValue(source) && !IsNullableValue(target)) return false;
        source = Underlying(source); target = Underlying(target);
        if (SymbolEqualityComparer.Default.Equals(source, target)) return true;
        return source.SpecialType is SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_Int32 &&
            target.SpecialType is SpecialType.System_Int64 or SpecialType.System_Decimal or SpecialType.System_Double ||
            source.SpecialType == SpecialType.System_Int64 && target.SpecialType == SpecialType.System_Decimal ||
            source.SpecialType == SpecialType.System_Single && target.SpecialType == SpecialType.System_Double;
    }
    private static bool Scalar(ITypeSymbol type)
    {
        type = Underlying(type);
        return type.TypeKind == TypeKind.Enum || type.SpecialType is SpecialType.System_String or SpecialType.System_Boolean or SpecialType.System_Byte or
            SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or
            SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Decimal or SpecialType.System_Double or SpecialType.System_Single or SpecialType.System_Char ||
            type.ToDisplayString() is "System.DateTime" or "System.DateTimeOffset" or "System.Guid" or "System.DateOnly" or "System.TimeOnly" or "System.TimeSpan";
    }
    private static IEnumerable<IPropertySymbol> Properties(ITypeSymbol type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
                if (!property.IsStatic && !property.IsIndexer && property.DeclaredAccessibility == Accessibility.Public && seen.Add(property.Name) &&
                    !Ignored(property)) yield return property;
    }
    private static bool Ignored(IPropertySymbol property)
    {
        var attribute = property.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonIgnoreAttribute");
        if (attribute is null) return false;
        var condition = attribute.NamedArguments.FirstOrDefault(a => a.Key == "Condition").Value.Value;
        return condition is not int value || value != 0; // Never = 0. Conditional omission is not a static profile feature.
    }
    private static bool HasAttribute(ISymbol symbol, string name) => symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == name);
    private static string JsonName(IPropertySymbol property) => property.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() ==
        "System.Text.Json.Serialization.JsonPropertyNameAttribute")?.ConstructorArguments[0].Value as string ?? property.Name;
    private static bool Nullable(ITypeSymbol type) => IsNullableValue(type) || type.IsReferenceType && type.NullableAnnotation == NullableAnnotation.Annotated;
    private static bool IsNullableValue(ITypeSymbol type) => type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
    private static ITypeSymbol Underlying(ITypeSymbol type) => IsNullableValue(type) ? ((INamedTypeSymbol)type).TypeArguments[0] : type;
    private static string TypeName(ITypeSymbol type) => type.ToDisplayString(TypeFormat);
    private static string Id(string name) => "@" + name;
    private static Node Unwrap(Node node) => node is BlockNode b && b.expressions.Count == 1 ? Unwrap(b.expressions[0]) : node;
    private static void Plain(Node node)
    {
        if (node.group is not null || node.stages is { Count: > 0 } || node.predicate is { Count: > 0 } || node.keepArray || node.tuple)
            throw new GenerationFailure(ExpressionError);
    }
    private sealed class GenerationFailure : Exception
    { internal DiagnosticDescriptor Descriptor { get; } internal GenerationFailure(DiagnosticDescriptor descriptor) => Descriptor = descriptor; }
}
