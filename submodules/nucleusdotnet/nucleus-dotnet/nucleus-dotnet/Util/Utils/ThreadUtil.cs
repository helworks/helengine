using System.Threading;

namespace Nucleus {
    public static class ThreadUtil {
        public static int MainThreadId { get; private set; }

        public static void Initialize() {
            MainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public static bool IsMainThread {
            get { return Thread.CurrentThread.ManagedThreadId == MainThreadId; }
        }
    }
}
