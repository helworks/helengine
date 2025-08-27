//********************************************************************************************
//Author: Sergey Stoyan, CliverSoft Co.
//        stoyan@cliversoft.com
//        sergey.stoyan@gmail.com
//        http://www.cliversoft.com
//        07 September 2006
//Copyright: (C) 2006, Sergey Stoyan
//********************************************************************************************
#if WINDOWS

using System;
using System.Threading;
//using System.Messaging;
using System.Text;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;

namespace Nucleus.Platform.Windows {
    public class EnumWindows {
        public List<Window> Windows = new List<Window>();

        /// <summary>
        /// enum all windows including child windows
        /// </summary>
		public EnumWindows() {
            Windows.Clear();

            Functions.EnumWindows(new Functions.EnumProc(this.enumWindowCallback), 0);
        }

        /// <summary>
        /// enum all windows including child windows
        /// </summary>
        public EnumWindows(IntPtr parentWindow) {
            Windows.Clear();

            Functions.EnumChildWindows(parentWindow, new Functions.EnumProc(this.enumChildWindowCallback), 0);
        }

        /// <summary>
        /// invoke building child window tree for give parent window
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="lValue"></param>
        /// <returns></returns>
		private bool enumWindowCallback(IntPtr hwnd, int lValue) {
            try {
                Window w = new Window();
                w.hwnd = hwnd;

                w.level = 0;
                w.parent_id = -1;

                StringBuilder s = new StringBuilder(255); ;

                Functions.GetClassName(hwnd, s, 255);
                w.class_name = s.ToString();
                Functions.GetWindowText(hwnd, s, 255);
                w.text = s.ToString();
                Functions.InternalGetWindowText(hwnd, s, 255);
                w.internal_text = s.ToString();
                w.path = "[" + w.text + "]";

                Windows.Add(w);

                Functions.EnumChildWindows(hwnd, new Functions.EnumProc(this.enumChildWindowCallback), Windows.Count - 1);
            } catch { }

            return true;
        }

        /// <summary>
        /// build child window tree recurcively
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="parent_id"></param>
        /// <returns></returns>
        bool enumChildWindowCallback(IntPtr hwnd, int parent_id) {
            try {
                Window w = new Window();
                w.hwnd = hwnd;
                w.level = ((Window)Windows[parent_id]).level + 1;
                w.parent_id = parent_id;

                StringBuilder s = new StringBuilder(255);

                Functions.GetClassName(hwnd, s, 255);
                w.class_name = s.ToString();
                Functions.GetWindowText(hwnd, s, 255);
                w.text = s.ToString();
                Functions.InternalGetWindowText(hwnd, s, 255);
                w.internal_text = s.ToString();
                w.path = ((Window)Windows[parent_id]).path + "[" + w.text + "]";

                Windows.Add(w);

                Functions.EnumChildWindows(hwnd, new Functions.EnumProc(this.enumChildWindowCallback), Windows.Count - 1);
            } catch { }

            return true;
        }
    }
}
#endif