namespace Nucleus.Threading {
    public class ThreadData {
        public int ThreadID { get; private set; }
        public ThreadTask CurrentTask;

        public ThreadData(int threadID) {
            ThreadID = threadID;
        }
    }
}
