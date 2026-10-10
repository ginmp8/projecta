using System;
using System.Threading;
using Projecta.Execution;
using Projecta.Json;
using Projecta.Impl;

namespace Projecta
{
    public sealed class ProjectaQuery
    {
        private readonly Node m_ast;
        private readonly string? m_query; 

        public static ProjectaQuery FromAst(Node ast)
        {
            Node optimized = Optimizer.OptimizeAst(ast);
            return new ProjectaQuery(optimized, null);
        }

        public ProjectaQuery(string queryText)
            : this(Parser.Parse(queryText), queryText)
        {
        }

        private ProjectaQuery(Node ast, string? query)
        {
            this.m_ast = ast;
            this.m_query = query;
        }

        public JToken Eval(JToken data, EvaluationEnvironment environment)
        {
            return Eval(data, environment, new EvaluationLimits(), default);
        }

        public JToken Eval(JToken data, EvaluationLimits limits, CancellationToken cancellationToken = default)
            => Eval(data, new EvaluationEnvironment(), limits, cancellationToken);

        public JToken Eval(JToken data, EvaluationEnvironment environment, EvaluationLimits limits,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(environment);
            ArgumentNullException.ThrowIfNull(limits);
            // Builtins initialize outside the budget so one tiny first call cannot poison static initialization.
            _ = EvaluationEnvironment.DefaultEnvironment;
            using var scope = EvaluationBudget.Enter(limits, cancellationToken);
            return JsonataQ.evaluateMain(this.m_ast, data, environment);
        }

        public override string ToString()
        {
            return this.m_query ?? this.m_ast.PrintAst();
        }

        public Node GetAst() 
        { 
            return this.m_ast; 
        }
    }

    public static class ProjectaExtensions
    {
        public static JToken Eval(this ProjectaQuery query, JToken input)
        {
            return query.Eval(input, bindings: null);
        }

        public static JToken Eval(this ProjectaQuery query, JToken input, JObject? bindings)
        {
            EvaluationEnvironment env;
            if (bindings != null)
            {
                env = new EvaluationEnvironment(bindings);
            }
            else
            {
                env = EvaluationEnvironment.DefaultEnvironment;
            }
            return query.Eval(input, env);
        }

        public static string Eval(this ProjectaQuery query, string dataJson, bool indentResult = true)
        {
            JToken data = JToken.Parse(dataJson, ParseSettings.DefaultSettings);
            JToken result = query.Eval(data);
            return indentResult ? result.ToIndentedString() : result.ToFlatString();
        }

        public static string Eval(this ProjectaQuery query, string dataJson)
        {
            JToken data = JToken.Parse(dataJson, ParseSettings.DefaultSettings);
            JToken result = query.Eval(data);
            return result.ToIndentedString();
        }
    }
}
