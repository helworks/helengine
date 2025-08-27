using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nucleus.ConsoleEngine {
    public abstract class ConsoleCommand {
        protected ConsoleManager consoleManager;

        public ConsoleCommand(ConsoleManager manager) {
            consoleManager = manager;
            CommandColor = ConsoleColor.White;
        }

        public virtual bool Hidden { get; }
        public virtual ConsoleColor CommandColor { get; }
        public virtual string Help { get; }
        public virtual ConsoleSubTaskManager SubTasks { get; }

        public abstract string Command { get; }
        public abstract CommandFeedback Execute(string[] args);
    }
}
