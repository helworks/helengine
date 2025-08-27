using Nucleus.Gaming;
using Nucleus.Gaming.Diagnostics;
using Nucleus.TaskManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Nucleus.TaskApp {
    class Program {
        static void Main(string[] args) {
            if (args.Length == 0) {
                Log.WriteLine("Invalid usage! Need arguments to proceed!", Palette.Error);
                return;
            }

            TaskData taskData = new TaskData(args[0]);
            Log.WriteLine($"Loading Assembly {taskData.AssemblyName}");
            Assembly assembly = Assembly.Load(taskData.AssemblyName);
            Log.WriteLine($"Loading Type {taskData.TypeName}");
            Type t = assembly.GetType(taskData.TypeName);
            Log.WriteLine($"Loading Function {taskData.FunctionName}");
            MethodInfo mInfo = t.GetMethod(taskData.FunctionName);
            Log.WriteLine($"Invoking Function {taskData.FunctionName}x({StringUtil.ArrayToString(taskData.Parameters)})");
            mInfo.Invoke(null, taskData.Parameters);
        }
    }
}
