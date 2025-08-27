using System;
using System.Runtime.InteropServices;

namespace Nucleus.Platform.Windows.Interop {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct ShFileInfo {
        public ShFileInfo(bool b) {
            hIcon = IntPtr.Zero;
            iIcon = 0;
            dwAttributes = 0;
            szDisplayName = "";
            szTypeName = "";
        }
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Shell32Interop.MAX_PATH)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Shell32Interop.MAX_TYPE)]
        public string szTypeName;
    };
}
