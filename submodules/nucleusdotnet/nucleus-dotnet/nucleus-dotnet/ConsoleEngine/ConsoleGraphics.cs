using System.Drawing;
using System.Text;
using System;

namespace Nucleus.ConsoleEngine {
    public class ConsoleGraphics {
        private struct ConsolePixel {
            public char Character;
            public ConsoleColor? ForegroundColor;
            public ConsoleColor? BackgroundColor;

            public ConsolePixel(char character, ConsoleColor? foregroundColor = null, ConsoleColor? backgroundColor = null) {
                Character = character;
                ForegroundColor = foregroundColor;
                BackgroundColor = backgroundColor;
            }
        }

        private ConsolePixel[,] canvas;
        private Size size;
        private Point offset;

        public ConsoleColor? BackgroundColor { get; set; }

        public ConsoleGraphics(int width, int height) {
            canvas = new ConsolePixel[width, height];
            size = new Size(width, height);

            Console.OutputEncoding = Encoding.UTF8;
        }

        private bool boundChecks(int x, int y) {
            if (x > this.size.Width ||
              y > this.size.Height) {
                return true;
            }
            return false;
        }

        public void SetOffset(int x, int y) {
            offset = new Point(x, y);
        }

        public void DrawString(string text, int startX, int startY, ConsoleColor? foregroundColor = null, ConsoleColor? backgroundColor = null) {
            if (string.IsNullOrEmpty(text)) {
                return;
            }

            startX += offset.X;
            startY += offset.Y;

            if (boundChecks(startX, startY)) {
                return;
            }

            int counter = 0;
            for (int x = startX; x < size.Width; x++) {
                canvas[x, startY] = new ConsolePixel(text[counter++], foregroundColor, backgroundColor); ;
                if (counter >= text.Length) {
                    return;
                }
            }
        }

        public void DrawStringPad(string text, int startX, int startY, ConsoleColor? foregroundColor = null, ConsoleColor? backgroundColor = null) {
            startX += offset.X;
            startY += offset.Y;
            startX -= text.Length;

            if (boundChecks(startX, startY)) {
                return;
            }

            int counter = 0;
            for (int x = startX; x < size.Width; x++) {
                canvas[x, startY] = new ConsolePixel(text[counter++], foregroundColor, backgroundColor); ;
                if (counter >= text.Length) {
                    return;
                }
            }
        }

        public void DrawHorizontalLine(char c, int x, int y, int width, ConsoleColor? foregroundColor = null, ConsoleColor? backgroundColor = null) {
            x += offset.X;
            y += offset.Y;

            if (boundChecks(x, y)) {
                return;
            }

            for (int i = 0; i < width; i++) {
                int xx = x + i;
                if (xx >= this.size.Width) {
                    continue;
                }
                canvas[xx, y] = new ConsolePixel(c, foregroundColor, backgroundColor);
            }
        }

        public void DrawVerticalLine(char c, int x, int y, int height, ConsoleColor? foregroundColor = null, ConsoleColor? backgroundColor = null) {
            x += offset.X;
            y += offset.Y;

            if (boundChecks(x, y)) {
                return;
            }

            for (int i = 0; i < height; i++) {
                int yy = y + i;
                if (yy >= this.size.Height) {
                    continue;
                }
                canvas[x, yy] = new ConsolePixel(c, foregroundColor, backgroundColor);
            }
        }



        public void DrawRectangle(int x, int y, int width, int height, ConsoleColor? foregroundColor = null, ConsoleColor? backgroundColor = null) {
            x += offset.X;
            y += offset.Y;

            if (boundChecks(x, y)) {
                return;
            }

            bool renderLeft = true;
            bool renderTop = true;
            if (x < 0) {
                width -= Math.Abs(x);
                renderLeft = false;
                x = 0;
            } else if (x > this.size.Width) {
                return;
            }

            if (y < 0) {
                height -= Math.Abs(y);
                renderTop = false;
                y = 0;
            }

            if (width < 0 || height < 0) {
                return;
            }

            if (x + width > size.Width) {
                // TODO: fix
                return;
            }

            int botY = y + height - 1;
            for (int pX = x + 1; pX < x + width; pX++) {
                if (pX >= this.size.Width) {
                    continue;
                }

                if (renderTop) {
                    canvas[pX, y] = new ConsolePixel('─', foregroundColor, backgroundColor);
                }

                if (botY < this.size.Height) {
                    canvas[pX, botY] = new ConsolePixel('─', foregroundColor, backgroundColor);
                }
            }

            int rightX = x + width;
            for (int pY = y; pY <= botY; pY++) {
                if (pY >= this.size.Height) {
                    continue;
                }
                if (renderLeft) {
                    canvas[x, pY] = new ConsolePixel('│', foregroundColor, backgroundColor);
                }

                if (rightX < this.size.Width) {
                    canvas[rightX, pY] = new ConsolePixel('│', foregroundColor, backgroundColor);
                }
            }

            canvas[x, y] = new ConsolePixel('┌', foregroundColor, backgroundColor);
            canvas[x + width, y] = new ConsolePixel('┐', foregroundColor, backgroundColor);
            canvas[x, y + height - 1] = new ConsolePixel('└', foregroundColor, backgroundColor);
            canvas[x + width, y + height - 1] = new ConsolePixel('┘', foregroundColor, backgroundColor);
        }

        public void FillRectangle(int x, int y, int width, int height, char c, ConsoleColor? foregroundColor = null, ConsoleColor? backgroundColor = null) {
            x += offset.X;
            y += offset.Y;

            for (int pX = x; pX < x + width; pX++) {
                if (pX >= this.size.Width) {
                    continue;
                }

                for (int pY = y; pY < y + height; pY++) {
                    if (pY >= this.size.Height) {
                        continue;
                    }
                    canvas[pX, pY] = new ConsolePixel(c, foregroundColor, backgroundColor);
                }
            }
        }

        public void Render() {
            Console.CursorVisible = false;
            Console.SetCursorPosition(0, 0);
            string buffer = "";

            ConsoleColor? setColor = null;
            ConsoleColor? setBgColor = null;

            if (BackgroundColor == null) {
                buffer += RESET_BACKGROUND;
            } else {
                buffer += ConsoleBackgroundColorToAnsi(BackgroundColor.Value);
            }

            for (int y = 0; y < size.Height; y++) {
                for (int x = 0; x < size.Width; x++) {
                    ConsolePixel pixel = canvas[x, y];

                    if (pixel.ForegroundColor != setColor) {
                        if (pixel.ForegroundColor == null) {
                            setColor = null;
                            buffer += "\x1b[39m";
                        } else {
                            string color = ConsoleColorToAnsi(pixel.ForegroundColor.Value);
                            buffer += color;
                            setColor = pixel.ForegroundColor;
                        }
                    }

                    if (pixel.BackgroundColor != setBgColor) {
                        if (pixel.BackgroundColor == null) {
                            setBgColor = null;
                            if (BackgroundColor == null) {
                                buffer += RESET_BACKGROUND;
                            } else {
                                buffer += ConsoleBackgroundColorToAnsi(BackgroundColor.Value);
                            }
                        } else {
                            string color = ConsoleBackgroundColorToAnsi(pixel.BackgroundColor.Value);
                            buffer += color;
                            setBgColor = pixel.BackgroundColor;
                        }
                    }

                    buffer += pixel.Character;
                    canvas[x, y] = new ConsolePixel(' ');
                }
            }

            Console.Write(buffer);
            Console.SetCursorPosition(0, 0);

            Console.ResetColor();
        }

        private static bool IsOutputRedirectedSafe() {
#if UNITY_EDITOR || UNITY_STANDALONE
        // Unity doesn’t have a console redirect concept, fake it
        return false;
#else
            try {
                return Console.IsOutputRedirected;
            } catch {
                return false;
            }
#endif
        }


        public static string ConsoleColorToAnsi(ConsoleColor color) {
            switch (color) {
                case ConsoleColor.Black: return "\x1b[30m";
                case ConsoleColor.DarkRed: return "\x1b[31m";
                case ConsoleColor.DarkGreen: return "\x1b[32m";
                case ConsoleColor.DarkYellow: return "\x1b[33m";
                case ConsoleColor.DarkBlue: return "\x1b[34m";
                case ConsoleColor.DarkMagenta: return "\x1b[35m";
                case ConsoleColor.DarkCyan: return "\x1b[36m";
                case ConsoleColor.Gray: return "\x1b[37m";
                case ConsoleColor.DarkGray: return "\x1b[90m";
                case ConsoleColor.Red: return "\x1b[91m";
                case ConsoleColor.Green: return "\x1b[92m";
                case ConsoleColor.Yellow: return "\x1b[93m";
                case ConsoleColor.Blue: return "\x1b[94m";
                case ConsoleColor.Magenta: return "\x1b[95m";
                case ConsoleColor.Cyan: return "\x1b[96m";
                case ConsoleColor.White: return "\x1b[97m";
                default: return "\x1b[39m";
            }
        }

        public static readonly string RESET_BACKGROUND = IsOutputRedirectedSafe() ? "" : "\x1b[49m";

        public static string ConsoleBackgroundColorToAnsi(ConsoleColor color) {
            switch (color) {
                case ConsoleColor.Black: return "\x1b[40m";
                case ConsoleColor.DarkRed: return "\x1b[41m";
                case ConsoleColor.DarkGreen: return "\x1b[42m";
                case ConsoleColor.DarkYellow: return "\x1b[43m";
                case ConsoleColor.DarkBlue: return "\x1b[44m";
                case ConsoleColor.DarkMagenta: return "\x1b[45m";
                case ConsoleColor.DarkCyan: return "\x1b[46m";
                case ConsoleColor.Gray: return "\x1b[47m";
                case ConsoleColor.DarkGray: return "\x1b[100m";
                case ConsoleColor.Red: return "\x1b[101m";
                case ConsoleColor.Green: return "\x1b[102m";
                case ConsoleColor.Yellow: return "\x1b[103m";
                case ConsoleColor.Blue: return "\x1b[104m";
                case ConsoleColor.Magenta: return "\x1b[105m";
                case ConsoleColor.Cyan: return "\x1b[106m";
                case ConsoleColor.White: return "\x1b[107m";
                default: return RESET_BACKGROUND;
            }
        }
    }
}
