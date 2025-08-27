#if WINDOWS
using System.Drawing;
using System.Windows.Forms;

namespace Nucleus.Platform.Windows.Controls {
    public class BorderTextBox : TextBox {
        private Color borderColor = Color.Black;
        public int BorderSize { get; set; } = 2;

        public Color BorderColor {
            get { return borderColor; }
            set {
                borderColor = value;
                this.Invalidate(); // Forces a repaint
            }
        }

        public BorderTextBox() {
            BorderStyle = BorderStyle.None; // Remove default border
            Padding = new Padding(BorderSize); // Make space for the border
        }

        protected override void WndProc(ref Message m) {
            base.WndProc(ref m);

            if (m.Msg == 0xF) { // WM_PAINT
                using (Graphics g = Graphics.FromHwnd(this.Handle)) {
                    using (Pen pen = new Pen(BorderColor, BorderSize)) {
                        g.DrawRectangle(pen, new Rectangle(0, 0, Width - 1, Height - 1));
                    }
                }
            }
        }
    }
}
#endif