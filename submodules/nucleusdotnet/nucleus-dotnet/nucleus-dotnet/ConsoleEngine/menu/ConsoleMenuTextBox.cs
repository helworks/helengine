using System;
using System.Drawing;

namespace Nucleus.ConsoleEngine {
    public class ConsoleMenuTextBox : ConsoleMenuOption {
        public bool IsPassword { get; set; }
        public string Input { get; set; } = "";

        private double timer;
        private bool tick;

        public ConsoleMenuTextBox(
            Point location,
            string text,
            Action callback,
            ConsoleColor? color = null,
            ConsoleColor? bgColor = null
        ) : base(location, text, callback, color, bgColor) {

        }

        public override void Render(ConsoleMenu menu, bool selected, double elapsed) {
            timer += elapsed;
            if (timer > 300) {
                timer = 0;
                tick = !tick;
            }

            string text = GetRenderText(menu);

            string input = Input;
            if (menu.Offset) {
                if (IsPassword) {
                    input = StringUtil.Repeat("*", input.Length);
                }
                input = " " + input;
            } else if (IsPassword) {
                input = StringUtil.Repeat("*", input.Length);
            }

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

                if (tick) {
                    input += "_";
                }

                menu.Graphics.DrawString(
                    input,
                    (Location.X * menu.Size.Width) + menu.Origin.X + offsetX,
                    (Location.Y * menu.Size.Height) + menu.Origin.Y + offsetY,
                    menu.SelectedColor,
                    menu.SelectedBgColor
                );

                menu.Graphics.DrawString(
                    text,
                    (Location.X * menu.Size.Width) + menu.Origin.X + offsetX,
                    (Location.Y * menu.Size.Height) + menu.Origin.Y + offsetY + 1,
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
                    input,
                    (Location.X * menu.Size.Width) + menu.Origin.X + offsetX,
                    (Location.Y * menu.Size.Height) + menu.Origin.Y + offsetY,
                    Color,
                    BackgroundColor
                );

                menu.Graphics.DrawString(
                    text,
                    (Location.X * menu.Size.Width) + menu.Origin.X + offsetX,
                    (Location.Y * menu.Size.Height) + menu.Origin.Y + offsetY + 1,
                    Color,
                    BackgroundColor
                );
            }
        }

        public override void ReceiveKey(ConsoleKey key) {
            base.ReceiveKey(key);

            if (key == ConsoleKey.Backspace && Input.Length > 0) {
                Input = Input.Remove(Input.Length - 1);
                return;
            } else if (key == ConsoleKey.Spacebar) {
                Input += " ";
            }

            string value = key.ToString().ToLowerInvariant();
            if (value.StartsWith("d") && value.Length == 2) {
                value = value.Substring(1);
            } else if (value.Length > 1) {
                return;
            }
            Input += value;
        }
    }
}
