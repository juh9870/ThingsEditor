using System;
using UnityEngine.Scripting;

namespace ThingsEditor.Scripting
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ApiModuleInitializer : PreserveAttribute { }
}