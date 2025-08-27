using System;
using System.Drawing;

namespace Nucleus.Platform.Windows.Interop {
    /// <summary>
    /// 
    /// </summary>
    public class Display {
        public Rectangle Bounds {
            get { return bounds; }
        }
        public string DeviceName {
            get { return deviceName; }
        }
        public bool Primary {
            get { return primary; }
        }
        public IntPtr Handle {
            get { return ptr; }
        }

        private Rectangle bounds;
        private readonly string deviceName;
        private readonly bool primary;
        private readonly IntPtr ptr;

        public Display(IntPtr pointer, Rectangle size, string device, bool isPrimary) {
            ptr = pointer;
            bounds = size;
            deviceName = device;
            primary = isPrimary;
        }
    }
}
