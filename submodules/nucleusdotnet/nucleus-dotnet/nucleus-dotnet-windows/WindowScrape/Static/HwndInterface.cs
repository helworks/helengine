using Microsoft.VisualBasic;
using Nucleus.Platform.Windows;
using Nucleus.Platform.Windows.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;

namespace Nucleus.WindowScrape {
    public static class HwndInterface {
        public static List<IntPtr> EnumHwnds() {
            var parent = IntPtr.Zero;
            return EnumChildren(parent);
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", ExactSpelling = true)]
        static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

        [DllImport("user32.dll")]
        static extern IntPtr GetLastActivePopup(IntPtr hWnd);

        private const uint GA_PARENT = 1; // Retrieves the parent window.This does not include the owner, as it does with the GetParent function.

        private const uint GA_ROOT = 2; // Retrieves the root window by walking the chain of parent windows.

        private const uint GA_ROOTOWNER = 3; // Retrieves the owned root window by walking the chain of parent and owner windows returned by GetParent.

        private static IntPtr GetLastVisibleActivePopUpOfWindow(IntPtr window) {
            IntPtr lastPopUp = GetLastActivePopup(window);

            if (IsWindowVisible(lastPopUp)) {
                return lastPopUp;
            } else if (lastPopUp == window) {
                return IntPtr.Zero;
            } else {
                return GetLastVisibleActivePopUpOfWindow(lastPopUp);
            }
        }

        public static bool KeepWindowHandleInAltTabList(Window window) {
            //http://stackoverflow.com/questions/210504/enumerate-windows-like-alt-tab-does
            //http://blogs.msdn.com/oldnewthing/archive/2007/10/08/5351207.aspx
            //1. For each visible window, walk up its owner chain until you find the root owner. 
            //2. Then walk back down the visible last active popup chain until you find a visible window.
            //3. If you're back to where you're started, (look for exceptions) then put the window in the Alt+Tab list.
            IntPtr root = GetAncestor(window.hwnd, GA_ROOTOWNER);

            if (GetLastVisibleActivePopUpOfWindow(root) == window.hwnd) {
                if (window.class_name == "Shell_TrayWnd" || // Windows taskbar
                    window.class_name == "DV2ControlHost" || // Windows startmenu, if open
                    window.class_name == "MsgrIMEWindowClass" || // Live messenger's notifybox i think
                    window.class_name == "SysShadow" || // Live messenger's shadow-hack
                    window.class_name == "DummyDWMListenerWindow" ||
                    window.class_name == "ThumbnailDeviceHelperWnd" ||
                    window.class_name == "WorkerW" ||
                    window.class_name == "Button" && window.text == "Start" ||
                    window.class_name == "EdgeUiInputTopWndClass" ||
                    window.class_name == "Windows.UI.Core.CoreWindow" ||
                    window.class_name == "Progman" ||
                    window.class_name == "CEF-OSC-WIDGET" ||
                    window.class_name == "ApplicationFrameWindow" ||
                    window.class_name.StartsWith("WMP9MediaBarFlyout")) { // WMP's "now playing" taskbar-toolbar)
                    return false;
                }

                return true;
            }
            return false;
        }

        public static IntPtr GetHwnd(string windowText, string className) {
            return (IntPtr)FindWindow(className, windowText);
        }

        public static IntPtr GetHwndFromTitle(string windowText) {
            return (IntPtr)FindWindow(null, windowText);
        }

        public static IntPtr GetHwndFromClass(string className) {
            return (IntPtr)FindWindow(className, null);
        }

        public static bool ActivateWindow(IntPtr hwnd) {
            return SetForegroundWindow(hwnd);
        }

        public static bool MinimizeWindow(IntPtr hwnd) {
            return CloseWindow(hwnd);
        }



        public static string GetHwndClassName(IntPtr hwnd) {
            var result = new StringBuilder(256);
            GetClassName(hwnd, result, result.MaxCapacity);
            return result.ToString();
        }
        public static int GetHwndTitleLength(IntPtr hwnd) {
            return GetWindowTextLength(hwnd);
        }
        public static string GetHwndTitle(IntPtr hwnd) {
            var length = GetHwndTitleLength(hwnd);
            var result = new StringBuilder(length + 1);
            GetWindowText(hwnd, result, result.Capacity);
            return result.ToString();
        }
        public static bool SetHwndTitle(IntPtr hwnd, string text) {
            return SetWindowText(hwnd, text);
        }
        public static string GetHwndText(IntPtr hwnd) {
            var len = (int)SendMessage(hwnd, (UInt32)WM.GETTEXTLENGTH, 0, 0) + 1;
            var sb = new StringBuilder(len);
            SendMessage(hwnd, (UInt32)WM.GETTEXT, (uint)len, sb);
            return sb.ToString();
        }
        public static void SetHwndText(IntPtr hwnd, string text) {
            SendMessage(hwnd, (UInt32)WM.SETTEXT, 0, text);
        }
        public static bool SetHwndPos(IntPtr hwnd, int x, int y) {
            return SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, (uint)(PositioningFlags.SWP_NOSIZE | PositioningFlags.SWP_NOZORDER));
        }
        public static bool SetHwndPosTopMost(IntPtr hwnd, int x, int y) {
            return SetWindowPos(hwnd, new IntPtr(-1), x, y, 0, 0, (uint)PositioningFlags.SWP_NOSIZE);
        }
        public static Point GetHwndPos(IntPtr hwnd) {
            var rect = new RECT();
            GetWindowRect(hwnd, out rect);
            var result = new Point(rect.Left, rect.Top);
            return result;
        }
        public static bool SetHwndSize(IntPtr hwnd, int w, int h) {
            return SetWindowPos(hwnd, IntPtr.Zero, 0, 0, w, h, (uint)(PositioningFlags.SWP_NOMOVE | PositioningFlags.SWP_NOZORDER));
        }
        public static bool SetHwndSizeTopMost(IntPtr hWnd, int w, int h) {
            return SetWindowPos(hWnd, new IntPtr(-1), 0, 0, w, h, (uint)PositioningFlags.SWP_NOMOVE);
        }

        private const int GWL_HWNDPARENT = -8;

        public static void SetOwner(IntPtr child, IntPtr owner) {
            SetWindowLong(child, GWL_HWNDPARENT, (long)owner);
        }

        public static void SetTopMost(IntPtr hWnd) {
            if (IsWindowVisible(hWnd)) {
                ShowWindow(hWnd, (int)WindowShowStyle.Hide);
                ShowWindow(hWnd, (int)WindowShowStyle.Restore);
            }
        }

        public static void MakeTopMost(IntPtr hWnd) {
            //SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, (uint)(PositioningFlags.SWP_NOSIZE | PositioningFlags.SWP_NOMOVE));
            //SetForegroundWindow(hWnd);
            //SetFocus(hWnd);
            //SetActiveWindow(hWnd);

            //ShowWindow(hWnd, (int)WindowShowStyle.Hide);
            //SetWindowPos(hWnd, HWND_TOP, 0, 0, 0, 0, (uint)(PositioningFlags.SWP_SHOWWINDOW | PositioningFlags.SWP_NOSIZE | PositioningFlags.SWP_NOMOVE));
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, (uint)(PositioningFlags.SWP_SHOWWINDOW | PositioningFlags.SWP_NOSIZE | PositioningFlags.SWP_NOMOVE));
        }

        public static bool MakeNotTopMost(IntPtr hWnd) {
            return SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, (uint)(PositioningFlags.SWP_NOSIZE | PositioningFlags.SWP_NOMOVE));
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        public static Size GetHwndSize(IntPtr hwnd) {
            var rect = new RECT();
            GetWindowRect(hwnd, out rect);
            var result = new Size(rect.Right - rect.Left, rect.Bottom - rect.Top);
            return result;
        }

        public static Size GetHwndClientSize(IntPtr hwnd) {
            var rect = new RECT();
            GetClientRect(hwnd, out rect);
            var result = new Size(rect.Right - rect.Left, rect.Bottom - rect.Top);
            return result;
        }

        public static bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h) {
            return MoveWindow(hWnd, x, y, w, h, true);
        }

        public static List<IntPtr> EnumChildren(IntPtr hwnd) {
            var child = IntPtr.Zero;
            var results = new List<IntPtr>();
            do {
                child = FindWindowEx(hwnd, child, null, null);
                if (child != IntPtr.Zero) results.Add(child);
            } while (child != IntPtr.Zero);
            return results;
        }

        public static IntPtr GetHwndChild(IntPtr hwnd, string clsName, string ctrlText) {
            return FindWindowEx(hwnd, IntPtr.Zero, clsName, ctrlText);
        }
        public static IntPtr GetHwndParent(IntPtr hwnd) {
            return GetParent(hwnd);
        }
        public static int SendMessage(IntPtr hwnd, WM msg, uint param1, uint param2) {
            return (int)SendMessage(hwnd, (uint)msg, param1, param2);
        }
        public static int SendMessage(IntPtr hwnd, WM msg, uint param1, string param2) {
            return (int)SendMessage(hwnd, (uint)msg, param1, param2);
        }
        public static string GetMessageString(IntPtr hwnd, WM msg, uint param) {
            var sb = new StringBuilder(65536);
            SendMessage(hwnd, (uint)msg, param, sb);
            return sb.ToString();
        }
        public static int GetMessageInt(IntPtr hwnd, WM msg) {
            return (int)SendMessage(hwnd, (uint)msg, 0, 0);
        }


        public static void MouseEvent(Point pos, MouseEventFlags value) {
            mouse_event
                ((int)value,
                 pos.X,
                 pos.Y,
                 0,
                 0);
        }

        const int MK_LBUTTON = 1;

        public static void ClickHwnd(IntPtr hwnd) {
            SendMessage(hwnd, (uint)WM.BN_CLICKED, IntPtr.Zero, IntPtr.Zero);
        }

        public static int MAKELPARAM(int p, int p_2) {
            return ((p_2 << 16) | (p & 0xFFFF));
        }

        public static void ClickWnd(IntPtr hwnd, int x, int y) {
            SendMessage(hwnd, WM.LBUTTONDOWN, MK_LBUTTON, (uint)MAKELPARAM(x, y));
            //SendMessage(hwnd, WM.LBUTTONUP, MK_LBUTTON, (uint)MAKELPARAM(x, y));
        }

        public static void ClickReleaseWnd(IntPtr hwnd, int x, int y) {
            SendMessage(hwnd, WM.LBUTTONUP, MK_LBUTTON, (uint)MAKELPARAM(x, y));
            //SendMessage(hwnd, WM.LBUTTONUP, MK_LBUTTON, (uint)MAKELPARAM(x, y));
        }

        public static void HighlightWnd(IntPtr hwnd, int x, int y) {
            SendMessage(hwnd, WM.MOUSEMOVE, 0, (uint)MAKELPARAM(x, y));
        }

        public static Point GetTitleBarSize(IntPtr hwnd) {
            RECT rcClient, rcWind;
            GetClientRect(hwnd, out rcClient);
            GetWindowRect(hwnd, out rcWind);

            Point ptDiff = new Point();
            ptDiff.X = (rcWind.Right - rcWind.Left) - rcClient.Right;
            ptDiff.Y = (rcWind.Bottom - rcWind.Top) - rcClient.Bottom;
            return ptDiff;
        }

        public static IntPtr FindTrayToolbarWindow(out IntPtr shellPtr) {
            IntPtr hWnd = FindWindow("Shell_TrayWnd", null);
            if (hWnd == IntPtr.Zero) {
                throw new Exception("Did not find Shell_TrayWnd");
            }
            shellPtr = hWnd;

            hWnd = FindWindowEx(hWnd, IntPtr.Zero, "TrayNotifyWnd", "");
            if (hWnd == IntPtr.Zero) {
                throw new Exception("Did not find TrayNotifyWnd");
            }

            hWnd = FindWindowEx(hWnd, IntPtr.Zero, "SysPager", "");
            if (hWnd == IntPtr.Zero) {
                throw new Exception("Did not find SysPager");
            }

            hWnd = FindWindowEx(hWnd, IntPtr.Zero, "ToolbarWindow32", null);
            if (hWnd == IntPtr.Zero) {
                throw new Exception("Did not find ToolbarWindow32");
            }

            return hWnd;
        }

        public static IntPtr FindHiddenTrayToolbarWindow() {
            IntPtr hWnd = FindWindow("NotifyIconOverflowWindow", null);
            if (hWnd == IntPtr.Zero) {
                throw new Exception("Did not find NotifyIconOverflowWindow");
            }

            hWnd = FindWindowEx(hWnd, IntPtr.Zero, "ToolbarWindow32", null);
            if (hWnd == IntPtr.Zero) {
                throw new Exception("Did not find ToolbarWindow32");
            }

            return hWnd;
        }

        public struct TBBUTTONINFO64 {
            public uint cbSize;
            public uint dwMask;
            public int idCommand;
            public int iImage;
            public byte fsState;
            public byte fsStyle;
            public short cx;
            public ulong lParam;
            public ulong pszText;
            public int cchText;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct ICONDATA {
            public IntPtr hWnd;
            public uint uID;
            public uint uCallbackMessage; // not sure
            public int dwState; // not sure
            public uint uVersion; // not sure
            public IntPtr hIcon;
            public IntPtr hBalloonIcon;
            public long nIconDemoteTimerID;
            public int dwUserPref;
            public int dwLastSoundTime;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szIconText;
            public uint uNumSeconds;
            public Guid guidItem;
        }

        public enum NotifyIconMessage : int {
            NIM_ADD = 0x00000000,
            NIM_MODIFY = 0x00000001,
            NIM_DELETE = 0x00000002,
            NIM_SETFOCUS = 0x00000003,
            NIM_SETVERSION = 0x00000004,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NotifyIconData {
            public System.Int32 cbSize; // DWORD
            public System.IntPtr hWnd; // HWND
            public System.Int32 uID; // UINT
            public NotifyIconMessage uFlags; // UINT
            public System.Int32 uCallbackMessage; // UINT
            public System.IntPtr hIcon; // HICON
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public System.String szTip; // char[128]
            public System.Int32 dwState; // DWORD
            public System.Int32 dwStateMask; // DWORD
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public System.String szInfo; // char[256]
            public System.Int32 uTimeoutOrVersion; // UINT
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public System.String szInfoTitle; // char[64]
            public System.Int32 dwInfoFlags; // DWORD
                                             //GUID guidItem; > IE 6
        }

        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RELEASE = 0x8000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint PAGE_READWRITE = 4;

        public const int TBIF_IMAGE = 0x00000001;
        public const int TBIF_TEXT = 0x00000002;
        public const int TBIF_STATE = 0x00000004;
        public const int TBIF_STYLE = 0x00000008;
        public const int TBIF_LPARAM = 0x00000010;
        public const int TBIF_COMMAND = 0x00000020;
        public const int TBIF_SIZE = 0x00000040;
        public const uint TBIF_BYINDEX = 0x80000000; // this specifies that the wparam in Get/SetButtonInfo is an index, not id

        public const int WM_USER = 0x0400;

        public const int TB_BUTTONCOUNT = (WM_USER + 24);
        public const int TB_GETBUTTONINFOW = (WM_USER + 63);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TBBUTTON {
            public int iBitmap;
            public int idCommand;
            public byte fsState;
            public byte fsStyle;
            public byte bReserved0;
            public byte bReserved1;
            public int dwData;
            public int iString;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct TRAYDATA {
            public IntPtr hwnd;
            public uint uID;
            public uint uCallbackMessage;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public uint[] Reserved;
            public IntPtr hIcon;
        };

        public static List<ICONDATA> GetTrayToolbarData(IntPtr hTrayWnd, string processName = null) {
            int procId;
            User32Interop.GetWindowThreadProcessId(hTrayWnd, out procId);

            int count = (int)SendMessage(hTrayWnd, WM.TB_BUTTONCOUNT, 0, 0);

            List<ICONDATA> icons = new List<ICONDATA>();
            ProcessSharedData data = new ProcessSharedData((int)procId, (uint)Marshal.SizeOf<TBBUTTON>());

            for (int i = 0; i < count; i++) {
                ProcessSharedData infoData = new ProcessSharedData(data.ProcessHandle, (uint)Marshal.SizeOf<TBBUTTONINFO64>());

                TBBUTTONINFO64 buttonInfo = new TBBUTTONINFO64();
                buttonInfo.cbSize = (uint)Marshal.SizeOf(typeof(TBBUTTONINFO64));
                buttonInfo.dwMask = (TBIF_BYINDEX | TBIF_IMAGE | TBIF_COMMAND | TBIF_LPARAM | TBIF_STATE);
                IntPtr ptrButtonInfo = Marshal.AllocHGlobal(Marshal.SizeOf(buttonInfo));
                Marshal.StructureToPtr(buttonInfo, ptrButtonInfo, false);
                infoData.WriteData(ptrButtonInfo);

                int nRet = SendMessage(hTrayWnd, (WM)TB_GETBUTTONINFOW, (uint)i, (uint)infoData.GetData());

                infoData.ReadData(ptrButtonInfo);
                buttonInfo = (TBBUTTONINFO64)Marshal.PtrToStructure(ptrButtonInfo, typeof(TBBUTTONINFO64));

                ICONDATA iconData = new ICONDATA();
                IntPtr ptrIconData = Marshal.AllocHGlobal(Marshal.SizeOf(iconData));
                Marshal.StructureToPtr(iconData, ptrIconData, false);
                infoData.ReadData(ptrIconData, (IntPtr)buttonInfo.lParam, Marshal.SizeOf<ICONDATA>());

                iconData = (ICONDATA)Marshal.PtrToStructure(ptrIconData, typeof(ICONDATA));

                string filename = Path.GetFileNameWithoutExtension(iconData.szExeName);
                if (!string.IsNullOrEmpty(processName)) {
                    if (filename.ToLowerInvariant().StartsWith(processName)) {
                        icons.Add(iconData);
                    }
                } else {
                    icons.Add(iconData);
                }

                infoData.Dispose();
            }

            data.Dispose();

            return icons;
        }

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        public static readonly IntPtr HWND_TOP = new IntPtr(0);
        public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        [Flags]
        public enum MouseEventFlags {
            LeftDown = 0x00000002,
            LeftUp = 0x00000004,
            MiddleDown = 0x00000020,
            MiddleUp = 0x00000040,
            Move = 0x00000001,
            Absolute = 0x00008000,
            RightDown = 0x00000008,
            RightUp = 0x00000010
        }

        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowA(string lpClassName, string lpWindowName);

        // Standard interface
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        private static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

        // Sending messages by string
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        private static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, uint wParam, string lParam);

        // Retrieving string data
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        private static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, uint wParam, StringBuilder lParam);

        // Retrieving numeric data
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        private static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, uint wParam, uint lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        /// <summary>
        /// Changes an attribute of the specified window. The function also sets the 32-bit (long) value at the specified offset into the extra window memory.
        /// </summary>
        /// <param name="hWnd">A handle to the window and, indirectly, the class to which the window belongs..</param>
        /// <param name="nIndex">The zero-based offset to the value to be set. Valid values are in the range zero through the number of bytes of extra window memory, minus the size of an integer. To set any other value, specify one of the following values: GWL_EXSTYLE, GWL_HINSTANCE, GWL_ID, GWL_STYLE, GWL_USERDATA, GWL_WNDPROC </param>
        /// <param name="dwNewLong">The replacement value.</param>
        /// <returns>If the function succeeds, the return value is the previous value of the specified 32-bit integer. 
        /// If the function fails, the return value is zero. To get extended error information, call GetLastError. </returns>
        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, long dwNewLong);

        [DllImport("USER32.DLL")]
        private static extern bool SetWindowText(IntPtr hWnd, string lpString);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);


        [DllImport("user32.dll")]
        private static extern bool CloseWindow(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
