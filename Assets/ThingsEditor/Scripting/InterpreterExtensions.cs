using Miniscript;
using UnityEngine;

namespace ThingsEditor.Scripting
{
    public static class InterpreterExtensions
    {
        // ReSharper disable Unity.PerformanceAnalysis
        /// <summary>
        ///     Run the compiled code until we either reach the end, or we reach the
        ///     specified steps limit. If step limit is hit, exception is thrown but
        ///     state is unaffected, you may chose to ignore exception and continue
        ///     code execution
        ///     <br />
        ///     Note that this method first compiles the source code if it wasn't compiled
        ///     already, and in that case, may generate compiler errors.  And of course
        ///     it may generate runtime errors while running.  In either case, these are
        ///     reported via errorOutput.
        /// </summary>
        /// <param name="stepsLimit">maximum amount of steps to run before returning</param>
        public static void Run(this Interpreter interpreter, int stepsLimit = 1000000)
        {
            try
            {
                if (interpreter.vm == null)
                {
                    interpreter.Compile();
                    if (interpreter.vm == null) return; // (must have been some error)
                }

                var steps = 0;
                while (!interpreter.vm.done && !interpreter.vm.yielding)
                {
                    if (steps++ > stepsLimit)
                        throw new RuntimeException(
                            $"Code execution exceeded {stepsLimit} steps! Check your code for potential infinite loops.");
                    interpreter.vm.Step(); // update the machine
                }
            }
            catch (MiniscriptException mse)
            {
                Debug.LogError(mse);
                interpreter.errorOutput.Invoke(mse.Description());
                interpreter.Stop(); // was: vm.GetTopContext().JumpToEnd();
                throw;
            }
        }
    }
}
