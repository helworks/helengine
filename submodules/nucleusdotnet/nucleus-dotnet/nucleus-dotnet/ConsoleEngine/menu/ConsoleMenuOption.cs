using System;
using System.Drawing;

namespace Nucleus.ConsoleEngine {
    public class ConsoleMenuOption {
        public Point Location { get; set; }
        public string Text { get; set; }

        public ConsoleColor? Color { get; set; }
        public ConsoleColor? BackgroundColor { get; set; }
        public ConsoleColor? BorderColor { get; set; }

        public Action Callback { get; set; }

        public ConsoleMenuOption(
            Point location,
            string text,
            Action callback,
            ConsoleColor? color = null,
            ConsoleColor? bgColor = null
        ) {
            Location = location;
            Text = text;
            Color = color;
            BackgroundColor = bgColor;
            Callback = callback;
            BorderColor = ConsoleColor.White;
        }

        protected string GetRenderText(ConsoleMenu menu) {
            string text = Text;
            if (menu.Offset) {
                text = " " + text;
            }

            if (menu.FillText) {
                text += StringUtil.Repeat(" ", menu.Size.Width - text.Length - 1);
            }

            return text;
        }

        public virtual void Render(ConsoleMenu menu, bool selected, double elapsed) {
            string text = GetRenderText(menu);

            int offsetX = 0;
            int offsetY = 0;
            if (selected) {
                menu.Graphics.FillRectangle(
                    (Location.X * menu.Size.Width) + menu.Origin.X,
                    (Location.Y * menu.Size.Height) + menu.Origin.Y,
                    menu.Size.Width,
                    menu.Size.Height,
                    ' ',
                    menu.SelectedColor,
                    menu.SelectedBgColor
                );

                if (BorderColor != null) {
                    menu.Graphics.DrawRectangle(
                        (Location.X * menu.Size.Width) + menu.Origin.X,
                        (Location.Y * menu.Size.Height) + menu.Origin.Y,
                        menu.Size.Width,
                        menu.Size.Height,
                        BorderColor.Value,
                        menu.SelectedBgColor
                    );
                    offsetX = 1;
                    offsetY = 1;
                }

                menu.Graphics.DrawString(
                    text,
                    (Location.X * menu.Size.Width) + menu.Origin.X + offsetX,
                    (Location.Y * menu.Size.Height) + menu.Origin.Y + offsetY,
                    menu.SelectedColor,
                    menu.SelectedBgColor
                );
            } else {
                if (BorderColor != null) {
                    menu.Graphics.DrawRectangle(
                        (Location.X * menu.Size.Width) + menu.Origin.X,
                        (Location.Y * menu.Size.Height) + menu.Origin.Y,
                        menu.Size.Width,
                        menu.Size.Height,
                        BorderColor.Value,
                        BackgroundColor
                    );
                    offsetX = 1;
                    offsetY = 1;
                }

                menu.Graphics.DrawString(
                    text,
                    (Location.X * menu.Size.Width) + menu.Origin.X + offsetX,
                    (Location.Y * menu.Size.Height) + menu.Origin.Y + offsetY,
                    Color,
                    BackgroundColor
                );
            }
        }

        public virtual void ReceiveKey(ConsoleKey key) {
        }
    }
}
