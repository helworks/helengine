using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Nucleus.ConsoleEngine {
    public class ConsoleMenu {
        public Point Origin { get; set; }
        public Size Size { get; set; }
        public List<ConsoleMenuOption> Options { get; set; }

        public Point Selected { get; set; }

        public ConsoleColor SelectedColor { get; set; }
        public ConsoleColor SelectedBgColor { get; set; }

        public bool FillText { get; set; } = true;
        public bool Offset { get; set; } = true;

        public ConsoleGraphics Graphics { get; private set; }

        public ConsoleMenu(ConsoleGraphics graphics) {
            this.Graphics = graphics;

            Options = new List<ConsoleMenuOption>();
            Size = new Size(30, 1);

            SelectedColor = ConsoleColor.White;
            SelectedBgColor = ConsoleColor.Magenta;
        }

        public virtual void OnShow() {

        }

        public void Render(double elapsed) {
            for (int i = 0; i < Options.Count; i++) {
                ConsoleMenuOption option = Options[i];
                option.Render(this, option.Location == Selected, elapsed);
            }
        }

        public void ReceiveInput(ConsoleKey key) {
            if (key == ConsoleKey.UpArrow) {
                // search for cloest value below
                ConsoleMenuOption current = Options.Find(c => c.Location == Selected);
                if (current == null) {
                    return;
                }

                List<ConsoleMenuOption> ordered = Options.OrderBy(c => c.Location.Y).ToList();
                int index = ordered.IndexOf(current);

                if (index == 0 || Options.Count == 1) {
                    return;
                }

                Selected = ordered[index - 1].Location;
            } else if (key == ConsoleKey.DownArrow) {
                ConsoleMenuOption current = Options.Find(c => c.Location == Selected);
                if (current == null) {
                    return;
                }

                List<ConsoleMenuOption> ordered = Options.OrderBy(c => c.Location.Y).ToList();
                int index = ordered.IndexOf(current);

                if (index == Options.Count - 1 || Options.Count == 1) {
                    return;
                }

                Selected = ordered[index + 1].Location;
            } else if (key == ConsoleKey.LeftArrow) {
                ConsoleMenuOption current = Options.Find(c => c.Location == Selected);
                if (current == null) {
                    return;
                }

                List<ConsoleMenuOption> ordered = Options.OrderBy(c => c.Location.X).ToList();
                int index = ordered.IndexOf(current);

                if (index == Options.Count - 1 || Options.Count == 1) {
                    return;
                }

                Selected = ordered[index + 1].Location;
            } else if (key == ConsoleKey.RightArrow) {
                ConsoleMenuOption current = Options.Find(c => c.Location == Selected);
                if (current == null) {
                    return;
                }

                List<ConsoleMenuOption> ordered = Options.OrderBy(c => c.Location.X).ToList();
                int index = ordered.IndexOf(current);

                if (index == Options.Count - 1 || Options.Count == 1) {
                    return;
                }

                Selected = ordered[index + 1].Location;
            } else if (key == ConsoleKey.Enter) {
                ConsoleMenuOption current = Options.Find(c => c.Location == Selected);
                if (current != null && current.Callback != null) {
                    current.Callback();
                }
            } else {
                ConsoleMenuOption current = Options.Find(c => c.Location == Selected);
                current?.ReceiveKey(key);
            }
        }
    }
}
