using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Nucleus.Diagnostics {
    /// <summary>
    /// Custom Text Writer that allows the Panel renderer to ignore cursor movements
    /// </summary>
    public class PanelTextWriter : TextWriter {
        public StreamWriter Original { get { return original; } }

        public string LineClear { get; private set; }
        public int CurrentLine { get; set; }
        public int MaxLines { get; set; }
        public int PanelY { get; set; }
        public bool RenderingPanel { get; set; }

        public override Encoding Encoding {
            get {
                return Encoding.ASCII;
            }
        }

        private readonly object locker = new object();
        private readonly List<string> lines;
        private readonly StreamWriter original;
        private int backCounter;

        public PanelTextWriter() {
            lines = new List<string>();

            original = new StreamWriter(Console.OpenStandardOutput());
            original.AutoFlush = true;
            MaxLines = Console.WindowHeight - 3;
            PanelY = Console.WindowHeight + 1;

            LineClear = StringUtil.RepeatCharacter(' ', Console.WindowWidth);
        }

        public void PWrite(string value) {
            lock (locker) {
                original.Write(value);
            }
        }

        public void PWrite(string value, ConsoleColor color) {
            lock (locker) {
                ConsoleColor current = Console.ForegroundColor;
                Console.ForegroundColor = color;
                original.Write(value);
                Console.ForegroundColor = current;
            }
        }

        public void PWriteLine(string value) {
            lock (locker) {
                original.WriteLine(value);
            }
        }

        public override void Write(string value) {
            base.Write(value);

            if (RenderingPanel) {
                return;
            }

            original.Write(value);
        }

        public override void WriteLine(string value) {
            base.WriteLine(value);

            if (RenderingPanel) {
                return;
            }

            int startCursorX = Console.CursorLeft;
            int startCursorY = Console.CursorTop;

            lock (locker) {
                Console.SetCursorPosition(0, PanelY + CurrentLine);
                //Write(LineClear);
                Console.SetCursorPosition(0, PanelY + CurrentLine);
                Write(value);
                CurrentLine++;

                if (CurrentLine >= MaxLines) {
                    CurrentLine = MaxLines;
                    backCounter++;
                    Console.MoveBufferArea(0, PanelY, Console.WindowWidth, MaxLines + 1, 0, PanelY - 1);
                }

                Console.SetCursorPosition(startCursorX, startCursorY);
            }
        }
    }
}
