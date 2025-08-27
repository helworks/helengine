using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Nucleus.Diagnostics {
    /// <summary>
    /// Singleton class for writing log information to a text file
    /// </summary>
    public class Log {
        public static readonly long MaxSize = 1024 * 1024 * 1024; // 16mb max log file (overkill af)
        public static Log Instance {
            get {
                if (instance == null) {
                    new Log(true);
                }
                return instance;
            }
        }

        private static Log instance;
        private readonly string logPath;
        private readonly Stream logStream;
        private readonly StreamWriter writer;
        private OutputLevel consoleLevel;
        private readonly bool enableLogging;
        private readonly List<ILogNode> logCallbacks;
        private readonly object locker;
        private readonly object writeLineLock;

        public Log(bool enableLogging) {
            this.enableLogging = enableLogging;
            locker = new object();
            writeLineLock = new object();

            instance = this;
            logCallbacks = new List<ILogNode>();

            if (enableLogging) {
                logPath = GetLogPath();
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));

                logStream = new FileStream(GetLogPath(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                logStream.Position = logStream.Length; // keep writing from where we left of

                writer = new StreamWriter(logStream);
                consoleLevel = OutputLevel.Low;
            }
        }

        public static void WriteLine(Exception ex) {
            Instance.PLog(ex.Message, ConsoleColor.Gray, OutputLevel.Medium);
        }

        public static void RegisterForLogCallback(ILogNode node) {
            Instance.logCallbacks.Add(node);
        }

        public static void UnregisterForLogCallback(ILogNode node) {
            Instance.logCallbacks.Remove(node);
        }

        public static void SetConsoleOutputLevel(OutputLevel level) {
            instance.consoleLevel = level;
        }

        public static string ReadLine() {
            return Console.ReadLine();
        }

        public static void WriteLine() {
            Instance.PLog("", ConsoleColor.Gray, OutputLevel.Low);
        }

        public static void WriteLine(string str, ConsoleColor color = ConsoleColor.Gray, OutputLevel displayLevel = OutputLevel.Low) {
            Instance.PLog(str, color, displayLevel);
        }

        public static void WriteLine(object str, ConsoleColor color = ConsoleColor.Gray, OutputLevel displayLevel = OutputLevel.Low) {
            Instance.PLog(str.ToString(), color, displayLevel);
        }

        public void LogExceptionFile(string appDataPath, Exception ex) {
            string local = ApplicationUtil.GetAppDataPath();
            DateTime now = DateTime.Now;
            string file = string.Format("{0}{1}{2}_{3}{4}{5}", now.Day.ToString("00"), now.Month.ToString("00"), now.Year.ToString("0000"), now.Hour.ToString("00"), now.Minute.ToString("00"), now.Second.ToString("00")) + ".log";
            string path = Path.Combine(appDataPath, file);

            using (Stream stream = File.OpenWrite(path)) {
                using (StreamWriter writer = new StreamWriter(stream)) {
                    writer.WriteLine("[Header]");
                    writer.WriteLine(now.ToLongDateString());
                    writer.WriteLine(now.ToLongTimeString());
                    // writer.WriteLine($"{Globals.Name} v{Globals.Version}");
                    writer.WriteLine("[PC Specs]");

                    writer.WriteLine("[Message]");
                    writer.WriteLine(ex.Message);
                    writer.WriteLine("[Stacktrace]");
                    writer.WriteLine(ex.StackTrace);

                    for (int i = 0; i < logCallbacks.Count; i++) {
                        ILogNode node = logCallbacks[i];
                        try {
                            node.OnFailureLog(writer);
                        } catch {
                            writer.WriteLine("LogNode failed to log: " + node.ToString());
                        }
                    }
                }
            }

#if WINFORMS
            System.Windows.Forms.MessageBox.Show("Application crash. Log generated at Data/" + file);
            System.Windows.Forms.Application.Exit();
#endif
        }

        public void PLog(string str, ConsoleColor color, OutputLevel displayLevel) {
            if (displayLevel >= consoleLevel) {
                WriteLine(str, color);
            }

            if (enableLogging) {
                LogData log = new LogData(str, color, displayLevel);
                ThreadPool.QueueUserWorkItem(doLog, log);
            }
        }

        protected static string GetLogPath() {
            if (ApplicationUtil.IsGameTasksApp()) {
                return Path.Combine(ApplicationUtil.GetAppDataPath(), "gametasks.log");
            }
            return Path.Combine(ApplicationUtil.GetAppDataPath(), "app.log");
        }

        private void WriteLine(string str, ConsoleColor color) {
            lock (writeLineLock) {
                DateTime now = DateTime.Now;
                ConsoleColor startColor = Console.ForegroundColor;

                Console.ForegroundColor = color;
                Console.Write($"[{now.ToLongTimeString()}] ");
                Console.Write(str + Environment.NewLine);
                Console.ForegroundColor = startColor;
            }
        }

        private void doLog(object s) {
            lock (locker) {
                LogData data = (LogData)s;

                //, ConsoleColor color, OutputLevel displayLevel
                writer.WriteLine(data.String);
                writer.Flush();

                if (logStream.Position > MaxSize) {
                    logStream.Position = 0;// write on top
                }
            }
        }
    }
}
