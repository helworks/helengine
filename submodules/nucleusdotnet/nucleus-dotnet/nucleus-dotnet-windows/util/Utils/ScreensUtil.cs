using Nucleus.Platform.Windows;
using Nucleus.Platform.Windows.Interop;
using System.Drawing;

namespace SplitScreenMe.Core {
    public static class ScreensUtil {
        public static Rectangle[] AllScreensRec() {
#if WINDOWS
            //return GetSetup_Triple4kHorizontal();
            Display[] all = User32Util.GetDisplays();
            Rectangle[] rects = new Rectangle[all.Length];

            for (int i = 0; i < all.Length; i++) {
                rects[i] = all[i].Bounds;
            }

            return rects;
#else
            throw new Exception();
#endif
        }
    }
}
