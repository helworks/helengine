#if WINDOWS
using Nucleus.DPI;
using SplitScreenMe.Core;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Nucleus.Platform.Windows.Controls {
    /// <summary>
    /// Form that all other forms inherit from. Has all
    /// the default design parameters to have the Nucleus Coop look and feel
    /// </summary>
    public class BaseUserControl : UserControl {
        public BaseUserControl() {
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(54, 57, 63);
            ForeColor = Color.FromArgb(240, 240, 240);
            Margin = new Padding(4, 4, 4, 4);
            Name = "BaseUserControl";
            Text = "BaseUserControl";

            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.ResizeRedraw, true); // this is to avoid visual artifacts

            // create it here, else the designer will show the default windows font
            Font = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Point);
        }

        /// <summary>
        /// Removes the flickering from constantly painting, if needed
        /// </summary>
        public void RemoveFlicker() {
            //this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
            this.SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
        }
    }
}
#endif