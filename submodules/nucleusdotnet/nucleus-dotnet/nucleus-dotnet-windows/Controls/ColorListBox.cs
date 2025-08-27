using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Nucleus.Controls {
    public class ColorListBox : ListBox {
        public Color HighlightColor { get; set; }

        private Brush textBrush;

        public ColorListBox() {
            DrawMode = DrawMode.OwnerDrawFixed;
        }

        protected override void OnDrawItem(DrawItemEventArgs e) {
            base.OnDrawItem(e);

            if (textBrush == null) {
                textBrush = new SolidBrush(this.ForeColor);
            }

            if (e.Index < 0) {
                return;
            }

            //if the item state is selected them change the back color 
            if ((e.State & DrawItemState.Selected) == DrawItemState.Selected) {
                e = new DrawItemEventArgs(e.Graphics,
                                          e.Font,
                                          e.Bounds,
                                          e.Index,
                                          e.State ^ DrawItemState.Selected,
                                          e.ForeColor,
                                          HighlightColor);
            }

            // Draw the background of the ListBox control for each item.
            e.DrawBackground();
            // Draw the current item text
            e.Graphics.DrawString(this.Items[e.Index].ToString(), e.Font, textBrush, e.Bounds, StringFormat.GenericDefault);
            // If the ListBox has focus, draw a focus rectangle around the selected item.
            e.DrawFocusRectangle();
        }
    }
}
