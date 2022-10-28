using System;
using System.Collections.Generic;
using Miniscript;

namespace ThingsEditor.Scripting.Modules
{
    public class EvalApi : MiniscriptAPI
    {
        private static readonly Dictionary<string, Function> EvalCache = new();

        private static ValMap evalMap;

        private static readonly Parser EvalParser = new()
        {
            errorContext = "$eval"
        };

        [ApiModuleInitializer]
        private static void Initialize()
        {
            RegisterClass("eval", EvalMap);
            Intrinsics.FunctionType()["bindOuters"] = SelfMethod("", BindOutersFunc).Param("context").Build();
        }

        public static ValMap EvalMap()
        {
            if (evalMap != null) return evalMap;
            return evalMap = BuildClass(
                Method("compile", EvalBlockFunc).Param("code"),
                Method("compileLine", EvalLineFunc).Param("code")
            );
        }

        private static Function GetFunction(string code)
        {
            if (EvalCache.TryGetValue(code, out var func)) return func;
            EvalParser.Reset();
            EvalParser.Parse(code);
            return EvalCache[code] = EvalParser.CreateImport();
        }

        private static Intrinsic.Result BindOutersFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var ctx = context.GetLocal("context");
            if (context.self is not ValFunction func)
                throw new RuntimeException("'bindOuters' is called on non-function value");
            if (ctx is not ValMap ctxMap) throw new RuntimeException("'context' must be a map");
            var result = new ValFunction(func.function, ctxMap);
            return new Intrinsic.Result(result);
        }

        private static Intrinsic.Result EvalBlockFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            return Compile(context);
        }

        private static Intrinsic.Result EvalLineFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            return Compile(context, code => "return " + code);
        }

        private static Intrinsic.Result Compile(TAC.Context context, Func<string, string> codeWrapper = null)
        {
            var code = context.GetLocal("code");
            if (code is not ValString) throw new RuntimeException($"Can only eval strings. Got {code}");
            var wrappedCode = code.ToString();
            if (codeWrapper != null) wrappedCode = codeWrapper(wrappedCode);
            var bytecode = GetFunction(wrappedCode);
            var func = new ValFunction(bytecode);
            return new Intrinsic.Result(func);
        }
    }
}