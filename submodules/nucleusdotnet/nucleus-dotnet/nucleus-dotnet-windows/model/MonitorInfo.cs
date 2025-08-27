using Nucleus.Platform.Windows.Interop;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Nucleus.Platform.Windows {
    public class MonitorInfo {
        public bool IsPrimary { get; set; }
        public Vector2 ScreenSize { get; set; }
        public Rect MonitorArea { get; set; }
        public Rect WorkArea { get; set; }
        public string DeviceName { get; set; }
        public IntPtr Hmon { get; set; }
    }
}
