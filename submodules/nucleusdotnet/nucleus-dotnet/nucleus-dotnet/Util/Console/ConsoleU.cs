using Nucleus.Diagnostics;
using System;

namespace Nucleus {
    public static class ConsoleU {
        public static OutputLevel currentLevel = OutputLevel.High;
        private static readonly object locker = new object();

        public static bool WaitingForInput { get; private set; }

        public static string ReadLine() {
            WaitingForInput = true;
            Console.Write("> ");
            string line = Console.ReadLine();
            WaitingForInput = false;
            return line;
        }

        private static string CropLine(string line) {
            if (Console.IsOutputRedirected) {
                return line;
            }
            
            int maxSize = Console.WindowWidth;

            if (line.Length >= maxSize) {
                line = line.Remove(maxSize, line.Length - maxSize);//cut start
                //line = line.Remove(0, line.Length - maxSize);// cut end
            }
            return line;
        }

        public static void WriteLine(string line) {
            lock (locker) {
                line = CropLine(line);
                Console.WriteLine(line);
            }
        }

        public static void WriteLine(string line, ConsoleColor foreGroundColor) {
            lock (locker) {
                line = CropLine(line);
                ConsoleColor current = Console.ForegroundColor;
                Console.ForegroundColor = foreGroundColor;
                Console.WriteLine(line);
                Console.ForegroundColor = current;
            }
        }

        public static void Write(string line, ConsoleColor foreGroundColor) {
            lock (locker) {
                line = CropLine(line);
                ConsoleColor current = Console.ForegroundColor;
                Console.ForegroundColor = foreGroundColor;
                Console.Write(line);
                Console.ForegroundColor = current;
            }
        }

        public static void WriteLine(string line, ConsoleColor foreGroundColor, OutputLevel logLevel) {
            if (logLevel < currentLevel) {
                return;
            }

            lock (locker) {
                line = CropLine(line);
                ConsoleColor current = Console.ForegroundColor;
                Console.ForegroundColor = foreGroundColor;
                Console.WriteLine(line);
                Console.ForegroundColor = current;
            }
        }

        public static void WriteLine(string line, OutputLevel logLevel) {
            if (logLevel < currentLevel) {
                return;
            }

            lock (locker) {
                line = CropLine(line);
                Console.WriteLine(line);
            }
        }
    }
}
