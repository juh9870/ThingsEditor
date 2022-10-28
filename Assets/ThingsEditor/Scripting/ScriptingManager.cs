using System;
using System.Reflection;
using Miniscript;
using UnityEngine;

namespace ThingsEditor.Scripting
{
    public class ScriptingManager
    {
        private static readonly ValMap SharedGlobals = new();
        private static bool initialized;

        public static Interpreter NewInterpreter(string code = "")
        {
            var interpreter = new Interpreter
            {
                standardOutput = Debug.Log,
                errorOutput = Debug.LogError
            };
            InitializeStatic();
            interpreter.Reset(code);
            interpreter.Compile();
            interpreter.vm.globalContext.variables = SharedGlobals;
            return interpreter;
        }

        public static event Action OnReset;

        public static void Reset()
        {
            SharedGlobals.map.Clear();
            OnReset?.Invoke();
        }

        private static void Startup()
        {
            NewInterpreter("import \"/sys/startup.ms\"").Run();
        }
        
        // ReSharper disable Unity.PerformanceAnalysis
        private static void InitializeStatic()
        {
            if (initialized) return;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            foreach (var type in assembly.GetTypes())
            foreach (var m in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public |
                                              BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var attributes = m.GetCustomAttributes(typeof(ApiModuleInitializer), false);
                if (attributes.Length > 0) m.Invoke(null, null);
            }

            initialized = true;

            OnReset += Startup;
            Reset();
        }
    }
}
