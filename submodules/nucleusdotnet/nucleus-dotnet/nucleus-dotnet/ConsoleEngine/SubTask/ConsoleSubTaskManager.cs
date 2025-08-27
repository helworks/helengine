using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nucleus.ConsoleEngine {
    public class ConsoleSubTaskManager {
        private Dictionary<string, ConsoleCommand> tasks;

        public ConsoleSubTaskManager() {
            tasks = new Dictionary<string, ConsoleCommand>();
        }

        public void RegisterTask(string name, ConsoleCommand del) {
            tasks.Add(name, del);
        }

        public CommandFeedback Execute(string[] args) {
            if (args.Length < 2) {
                return CommandFeedback.WrongNumberOfArguments;
            }

            ConsoleCommand command = tasks[args[1]];

            string[] newArgs = new string[args.Length - 1];
            Array.Copy(args, 1, newArgs, 0, newArgs.Length);

            return command.Execute(newArgs);
        }

        public ConsoleCommand GetCommand(string cmd) {
            ConsoleCommand command;
            tasks.TryGetValue(cmd, out command);
            return command;
        }
    }
}
