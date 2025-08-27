#if WINDOWS
using System.Drawing;
using System.Windows.Forms;

namespace Nucleus.Platform.Windows.Controls {
    public class TransparentControl : Control {
        public TransparentControl() {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Opaque, true);
            this.BackColor = Color.Transparent;
        }

        protected override CreateParams CreateParams {
            get {
                CreateParams cp = base.CreateParams;
                cp.ExStyle = cp.ExStyle | 0x20;
                return cp;
            }
        }
    }
}
#endif