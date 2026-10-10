using Projecta.Eval;
using Projecta.Json;
using Projecta.Impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Projecta
{
    public sealed class EvaluationEnvironment
    {
        private static readonly EvaluationEnvironment Builtins;
        public static EvaluationEnvironment DefaultEnvironment => new EvaluationEnvironment();

        static EvaluationEnvironment()
        {
            Builtins = EvaluationEnvironment.CreateDefault();
        }

        internal static EvaluationEnvironment CreateDefault() //main parent, contains default function bindings
        {
            EvaluationEnvironment result = new EvaluationEnvironment(null, null);
            foreach (MethodInfo mi in typeof(BuiltinFunctions).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                result.BindFunction(mi);
            }
            return result;
        }

        //used at actual EvalProcessor.EvaluateJson start to inject EvaluationSupplement
        internal static EvaluationEnvironment CreateEvalEnvironment(EvaluationEnvironment parentEnvironment) 
        {
            EvaluationEnvironment result = new EvaluationEnvironment(parentEnvironment, new EvaluationSupplement());
            return result;
        }

        //used during evaluation when nesting
        internal static EvaluationEnvironment CreateNestedEnvironment(EvaluationEnvironment parent)
        {
            EvaluationEnvironment result = new EvaluationEnvironment(parent, parent.m_evaluationSupplement);
            return result;
        }

        private readonly Dictionary<string, JToken> m_bindings = new Dictionary<string, JToken>();
        private readonly EvaluationEnvironment? m_parent;
        private readonly EvaluationSupplement? m_evaluationSupplement;
        public int MaxDepth { get; set; } = 250;
        public int MaxTailRecursionIteration { get; set; } = 10000;

        private EvaluationEnvironment(EvaluationEnvironment? parent, EvaluationSupplement? evaluationSupplement)
        {
            this.m_parent = parent;
            if (this.m_parent != null)
            {
                this.MaxDepth = this.m_parent.MaxDepth;
                this.MaxTailRecursionIteration = this.m_parent.MaxTailRecursionIteration;
            }
            this.m_evaluationSupplement = evaluationSupplement;
        }

        //public version to provide for ProjectaQuery.Eval()
        public EvaluationEnvironment()
            : this(Builtins, null)
        {

        }

        public EvaluationEnvironment(JObject bindings)
            : this()
        {
            foreach (KeyValuePair<string, JToken> property in bindings.Properties)
            {
                this.BindValue(property.Key, property.Value);
            }
        }


        /// <summary>True when a name is reserved by the built-in environment.</summary>
        public static bool IsBuiltinName(string name) => Builtins.m_bindings.ContainsKey(name);

        /// <summary>Binds trusted synchronous host code. Authorization and result limits belong to the caller.</summary>
        public void BindHostFunction(string name, Func<IReadOnlyList<JToken>, JToken> callback,
            int minArguments, int maxArguments, string? signature = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(callback);
            if (minArguments < 0 || maxArguments < minArguments || maxArguments > 64)
                throw new ArgumentOutOfRangeException(nameof(maxArguments));
            if (IsBuiltinName(name)) throw new ArgumentException("Built-in names cannot be replaced by host functions.", nameof(name));
            this.m_bindings.Add(name, new HostFunctionToken(name, callback, minArguments, maxArguments, signature));
        }

        public void BindValue(string name, JToken value)
        {
            this.m_bindings[name] = value;  //allow overrides
        }

        public void BindFunction(MethodInfo mi)
        {
            FunctionSignatureAttribute? signAttr = mi.GetCustomAttribute<FunctionSignatureAttribute>();
            this.BindFunction(mi.Name, mi, signAttr?.Signature);
        }

        public void BindFunction(string name, MethodInfo mi, string? signature = null)
        {
            Signature? sign = signature != null? new Signature(signature) : null;
            this.m_bindings.Add(name, new FunctionTokenCsharp(name, mi, sign));
        }

        public void BindFunction(string name, Delegate funcDelegate, string? signature = null)
        {
            Signature? sign = signature != null ? new Signature(signature) : null;
            this.m_bindings.Add(name, new FunctionTokenCsharp(name, funcDelegate, sign));
        }

        internal JToken Lookup(string name)
        {
            if (this.m_bindings.TryGetValue(name, out JToken? result))
            {
                return result;
            }
            else if (this.m_parent != null)
            {
                return this.m_parent.Lookup(name);
            }
            else
            {
                return JsonataQ.UNDEFINED;
            }
        }

        internal EvaluationSupplement GetEvaluationSupplement()
        {
            if (this.m_evaluationSupplement == null)
            {
                throw new Exception($"Calling {nameof(GetEvaluationSupplement)}() at non-evaluation env. Should not happen");
            };
            return this.m_evaluationSupplement;
        }

        internal void IncDepth()
        {
            Projecta.Execution.EvaluationBudget.CheckpointCurrent();
            int depth = this.GetEvaluationSupplement().IncDepth();
            if (depth > Math.Min(this.MaxDepth, Projecta.Execution.EvaluationBudget.Current?.Limits.MaxDepth ?? 128))
            {
                throw new JsonataException(JsonataErrorCode.U1001, "Stack overflow error: Check for non-terminating recursive function.  Consider rewriting as tail-recursive.");
            }
        }

        internal void DecDepth()
        {
            this.GetEvaluationSupplement().DecDepth();
        }
    }
}
