using System;
using System.Collections.Generic;
using Miniscript;

namespace ThingsEditor.Scripting
{
    public class MiniscriptAPI
    {
        protected const string DefaultVarargsName = "arguments";
        protected static readonly ValString Handle = new("_handle");
        protected static readonly ValString DefaultInvocation = new("_defaultInvocation");
        protected static readonly ValUniqueSymbol UnsetParameter = new("Unset parameter");

        private static int lastParamId;
        private static readonly object Lock = new();

        private static bool DisallowAllAssignment(Value key, Value value)
        {
            throw new RuntimeException("Assignment to protected map");
        }

        protected static IntrinsicBuilder Method(string name, IntrinsicCode code = null,
            string globalIntrinsicName = "")
        {
            var f = Intrinsic.Create(globalIntrinsicName);
            var builder = new IntrinsicBuilder(f, name);
            if (code != null) builder.Code(code);
            return builder;
        }

        protected static IntrinsicBuilder SelfMethod(string name, IntrinsicCode code = null,
            string globalIntrinsicName = "")
        {
            return Method(name, code, globalIntrinsicName).Param("self");
        }

        protected static string VarargParamName(int id)
        {
            return "__p_" + id;
        }

        protected static IntrinsicBuilder VarargMethod(string name, int maxParams, IntrinsicCode code = null,
            string varargName = DefaultVarargsName, string globalIntrinsicName = "")
        {
            var startId = lastParamId;
            var method = Method(name, code, globalIntrinsicName).Wrap(wrappedCode => (context, result) =>
            {
                var paramsList = new ValList();
                for (var i = 0; i < maxParams; i++)
                {
                    var value = context.GetLocal(VarargParamName(i + startId));
                    if (value == UnsetParameter) break;
                    if (value is ValList list)
                        paramsList.values.AddRange(list.values);
                    else
                        paramsList.values.Add(value);
                }

                context.SetVar(varargName, paramsList);
                return wrappedCode(context, result);
            });

            lock (Lock)
            {
                for (var i = 0; i < maxParams; i++) method.Param(VarargParamName(lastParamId++), UnsetParameter);
            }

            return method;
        }

        protected static ValMap BuildClass(ValFunction defaultInvocation, params IntrinsicBuilder[] methods)
        {
            var map = BuildClass(methods);
            map[DefaultInvocation.ToString()] = defaultInvocation;
            return map;
        }

        protected static ValMap BuildClass(params IntrinsicBuilder[] methods)
        {
            ValMap classHolder = new()
            {
                assignOverride = DisallowAllAssignment
            };

            foreach (var method in methods) classHolder[method.Name] = method.Build();

            return classHolder;
        }

        protected static void RegisterClass(string name, Func<ValMap> provider, bool lazy = true)
        {
            var f = Intrinsic.Create(name);
            f.code = (_, _) => new Intrinsic.Result(provider());
            if (!lazy) provider();
        }

        /// <summary>
        ///     Constructs two-parts function. Main use case is to invoke manually pushed functions before continuing
        ///     <br />
        ///     Note that first function is required to return non-null partial result, otherwise detection will fail.
        /// </summary>
        /// <param name="first">Function that will executed first. Must return non-null partial result if not done</param>
        /// <param name="second">Function that will executed second. Must return done result</param>
        /// <returns></returns>
        protected static IntrinsicCode TwoStepFunction(IntrinsicCode first, IntrinsicCode second)
        {
            return (context, partialResult) =>
                partialResult == null ? first(context, null) : second(context, partialResult);
        }

        /// <summary>
        ///     Constructs multipart function. Main use case is to invoke manually pushed functions before continuing while
        ///     retaining context of previously called functions.
        ///     <br />
        ///     Note that function constructed this way is less performant than
        ///     <see cref="ThingsEditor.Scripting.MiniscriptAPI.MultiStepFunction" /> due to creating callbacks on every step.
        ///     If you are returning separate functions without context, consider using MultiStepFunction instead, unless
        ///     you need recursion.
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        protected static IntrinsicCode RecursiveMultiStepFunction(ChainedMultistepCode code)
        {
            return (context, partialResult) =>
            {
                ChainedMultistepCode next;
                Intrinsic.Result result = null;
                if (partialResult == null)
                {
                    next = code(context, out result);
                }
                else
                {
                    var wrapper = partialResult.result as ValWrapper;
                    next = wrapper?.content as ChainedMultistepCode;
                    next = next?.Invoke(context, out result);
                }

                if (next != null) return new Intrinsic.Result(new ValWrapper(next), false);
                if (result == null)
                    throw new Exception("Multi step function hasn't set a result by the end of the execution chain");
                return result;
            };
        }

        /// <summary>
        ///     Constructs multipart function. Main use case is to invoke manually pushed functions before continuing.
        ///     <br />
        ///     If you only have two members, it's highly recommended to use
        ///     <see cref="ThingsEditor.Scripting.MiniscriptAPI.TwoStepFunction" /> instead
        /// </summary>
        /// <param name="parts"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        protected static IntrinsicCode MultiStepFunction(params IntrinsicCode[] parts)
        {
            if (parts.Length == 0) throw new Exception("No code blocks are defined for the function");

            if (parts.Length == 1)
                throw new Exception(
                    "Only one code blocks is defined for the function, consider using that code block on its own");

            return (context, partialResult) =>
            {
                var indexValue = context.GetTemp(1, null);
                var index = 0;
                if (indexValue != null) index = indexValue.IntValue();
                var result = parts[index].Invoke(context, partialResult);
                if (result.done)
                    context.SetTemp(1, null);
                else if (index == parts.Length - 1)
                    throw new Exception("Function parts have come to an end without returning a done result");

                context.SetTemp(1, new ValNumber(index));
                return result;
            };
        }

        protected delegate ChainedMultistepCode ChainedMultistepCode(TAC.Context context, out Intrinsic.Result result);

        protected class IntrinsicBuilder
        {
            private readonly Intrinsic intrinsic;
            public readonly string Name;
            private readonly List<Func<IntrinsicCode, IntrinsicCode>> wrappers = new();
            private IntrinsicCode code;

            public IntrinsicBuilder(Intrinsic intrinsic, string name = null)
            {
                this.intrinsic = intrinsic;
                Name = name;
            }

            // ReSharper disable once ParameterHidesMember
            public IntrinsicBuilder Code(IntrinsicCode code)
            {
                this.code = code;
                return this;
            }

            public IntrinsicBuilder Wrap(Func<IntrinsicCode, IntrinsicCode> wrapper)
            {
                wrappers.Add(wrapper);
                return this;
            }

            public ValFunction Build()
            {
                var wrappedCode = code;
                for (var i = wrappers.Count - 1; i >= 0; i--) wrappedCode = wrappers[i](wrappedCode);

                intrinsic.code = wrappedCode;
                return intrinsic.GetFunc();
            }

            #region Params

            public IntrinsicBuilder Param(string name, string defaultValue)
            {
                intrinsic.AddParam(name, defaultValue);
                return this;
            }

            public IntrinsicBuilder Params(params (string name, string defaultValue)[] parameters)
            {
                foreach (var param in parameters) intrinsic.AddParam(param.name, param.defaultValue);

                return this;
            }

            public IntrinsicBuilder StringParams(params string[] parameters)
            {
                return StringParamsDefault("", parameters);
            }

            public IntrinsicBuilder StringParamsDefault(string defaultValue, params string[] parameters)
            {
                foreach (var param in parameters) intrinsic.AddParam(param, defaultValue);

                return this;
            }

            public IntrinsicBuilder Param(string name, double defaultValue)
            {
                intrinsic.AddParam(name, defaultValue);
                return this;
            }

            public IntrinsicBuilder Params(params (string name, double defaultValue)[] parameters)
            {
                foreach (var param in parameters) intrinsic.AddParam(param.name, param.defaultValue);

                return this;
            }

            public IntrinsicBuilder DoubleParams(params string[] parameters)
            {
                return DoubleParamsDefault(0, parameters);
            }

            public IntrinsicBuilder DoubleParamsDefault(double defaultValue = 0, params string[] parameters)
            {
                foreach (var param in parameters) intrinsic.AddParam(param, defaultValue);

                return this;
            }

            public IntrinsicBuilder Param(string name, Value defaultValue = null)
            {
                intrinsic.AddParam(name, defaultValue);
                return this;
            }

            public IntrinsicBuilder Params(params (string name, Value defaultValue)[] parameters)
            {
                foreach (var param in parameters) intrinsic.AddParam(param.name, param.defaultValue);

                return this;
            }

            public IntrinsicBuilder Params(params string[] parameters)
            {
                return ParamsDefault(null, parameters);
            }

            public IntrinsicBuilder ParamsDefault(Value defaultValue, params string[] parameters)
            {
                foreach (var param in parameters) intrinsic.AddParam(param, defaultValue);

                return this;
            }

            #endregion
        }
    }
}
