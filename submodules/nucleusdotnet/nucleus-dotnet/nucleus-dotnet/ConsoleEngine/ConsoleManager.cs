using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Nucleus.ConsoleEngine {
    public class ConsoleManager {
        private bool silentMode;
        protected bool running;
        public Dictionary<string, ConsoleCommand> commands;

        public void SetSilentMode(bool value) {
            silentMode = value;
        }

        public ConsoleManager() {
            commands = new Dictionary<string, ConsoleCommand>();
        }

        public void SearchFromLoadedAssemblies() {
            Type consoleType = typeof(ConsoleCommand);

            Assembly entryAssembly = Assembly.GetEntryAssembly();
            string folder = Path.GetDirectoryName(entryAssembly.Location);
            string[] arrDlls = Directory.GetFiles(folder, "*.dll");
            arrDlls = ArrayUtil.Join(arrDlls, Directory.GetFiles(folder, "*.exe"));

            //AssemblyName[] assNames = entryAssembly.GetReferencedAssemblies();
            //Assembly[] assemblies = new Assembly[] { entryAssembly };
            //for (int i = 0; i < assemblies.Length; i++) {

            for (int i = 0; i < arrDlls.Length; i++) {
                string strDllPath = arrDlls[i];
                //Assembly assembly = assemblies[i];

                try {
                    Assembly assembly = Assembly.LoadFrom(strDllPath);
                    Type[] types = assembly.GetTypes();
                    for (int j = 0; j < types.Length; j++) {
                        Type t = types[j];

                        if (t.IsSubclassOf(consoleType)) {
                            ConsoleCommand cmd = (ConsoleCommand)Activator.CreateInstance(t, this);
                            commands.Add(cmd.Command, cmd);
                        }
                    }
                } catch { }
            }
        }

        public bool InputYesNo(bool silent = true) {
            ConsoleU.WriteLine("Yes/No", Palette.Question);
            if (silentMode) {
                // still shows up that we were asking the user a question
                ConsoleU.WriteLine(silent ? "Yes" : "No", Palette.Question);
                ConsoleU.WriteLine("yes");
                return silent;
            }

            string yesno = ConsoleU.ReadLine().ToLower();
            return yesno.StartsWith("y");
        }

        public void ExecuteCommand(string line) {
            if (string.IsNullOrEmpty(line)) {
                return;
            }

            string[] sep = line.Split(' ');
            ExecuteCommand(sep);
        }

        public void ExecuteCommand(string[] sep) {
            if (sep.Length == 0) {
                return;
            }

            string first = sep[0];
            ConsoleCommand cmd;
            if (!commands.TryGetValue(first, out cmd)) {
                ConsoleU.WriteLine("Unknown command", Palette.Error);
                return;
            }

            CommandFeedback feedback = cmd.Execute(sep);
            if (feedback != CommandFeedback.Success) {
                ConsoleU.WriteLine(feedback.ToString(), Palette.Error);
            }
        }

        public void Run() {
            running = true;

            for (; ; ) {
                Tick();

                if (!running) {
                    break;
                }
            }
        }

        protected virtual void Tick() {
            string line = ReadLine();
            ExecuteCommand(line);
        }

        public ConsoleCommand GetCommand(string cmd) {
            ConsoleCommand command;
            commands.TryGetValue(cmd, out command);
            return command;
        }

        public string ReadLine() {
            Console.SetCursorPosition(1, Console.WindowHeight - 3);
            Console.Write("> ");

            string line = "";

            string word = "";
            ConsoleCommand cmd = null;
            for (; ; ) {
                ConsoleKeyInfo consoleKeyInfo = Console.ReadKey();
                ConsoleKey consoleKey = consoleKeyInfo.Key;
                char character = consoleKeyInfo.KeyChar;

                if (consoleKey == ConsoleKey.Enter) {
                    break;
                } else if (consoleKey == ConsoleKey.Spacebar) {
                    // new word, check if last word is a command
                    if (cmd == null) {
                        cmd = this.GetCommand(word);
                    } else {
                        // check if subtasks exist
                        cmd = cmd.SubTasks?.GetCommand(word);
                    }

                    if (cmd != null) {
                        // change colors
                        Console.SetCursorPosition(Console.CursorLeft - word.Length - 1, Console.CursorTop);
                        ConsoleU.Write(word + character, cmd.CommandColor);
                    }

                    word = "";
                } else if (consoleKey == ConsoleKey.Backspace) {
                    return ReadLine();

                    //Console.Write(" ");
                    //Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);

                    //line = line.Substring(line.Length - 1, 1);
                    //word = word.Substring(line.Length - 1, 1);
                }

                line += character;

                if (consoleKey != ConsoleKey.Spacebar) {
                    word += character;
                }
            }

            Console.WriteLine();
            return line;
        }
    }
}
