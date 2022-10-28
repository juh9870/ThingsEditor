using System.Collections.Generic;
using Miniscript;

namespace ThingsEditor.Scripting.Modules
{
    public class SystemApi : MiniscriptAPI
    {
        private static ValMap system;

        [ApiModuleInitializer]
        private static void Initialize()
        {
            RegisterClass("system", SystemModule, false);
        }

        public static ValMap SystemModule()
        {
            if (system != null) return system;

            return system = BuildClass(
                Method("exit", ExitFunc, "exit"),
                Method("raiseException", RaiseFunc).Param("message", ""),
                Method("stackTrace", StackTraceFunc, "stackTrace"),
                Method("wait", WaitFunc, "wait")
            );
        }

        private static Intrinsic.Result ExitFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            if (context.interpreter.Running())
                //interpreter.vm.globalContext.variables.SetElem(MiniMicroAPI._stackAtBreak, 
                //	MiniMicroAPI.StackList(interpreter.vm));
                context.interpreter.Stop();

            return Intrinsic.Result.Null;
        }

        private static Intrinsic.Result RaiseFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            throw new RuntimeException(context.GetLocalString("message"));
        }

        private static Intrinsic.Result StackTraceFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var stack = context.interpreter.vm.GetStack();
            var strings = new List<string>(stack.Count);
            for (var i = 0; i < stack.Count; i++)
            {
                var loc = stack[i];
                if (loc == null) continue;
                strings.Add(loc.ToString());
            }

            return new Intrinsic.Result(strings.ToValue());
        }

        private static Intrinsic.Result WaitFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var now = context.vm.runTime;
            if (partialResult == null)
            {
                // Just starting our wait; calculate end time and return as partial result
                var interval = context.GetLocalDouble("seconds");
                context.vm.yielding = true;
                return new Intrinsic.Result(new ValNumber(now + interval), false);
            }

            // Continue until current time exceeds the time in the partial result
            if (now > partialResult.result.DoubleValue()) return Intrinsic.Result.Null;
            context.vm.yielding = true;
            return partialResult;
        }
    }
}