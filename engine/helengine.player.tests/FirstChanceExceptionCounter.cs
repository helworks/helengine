using System.Runtime.ExceptionServices;

namespace helengine.player.tests {
    /// <summary>
    /// Counts first-chance exceptions raised on one thread, so a test can prove a code path is exception-free even while
    /// other test classes run in parallel on other threads.
    /// </summary>
    public sealed class FirstChanceExceptionCounter {
        /// <summary>Managed thread whose exceptions are counted.</summary>
        readonly int ThreadId;

        /// <summary>
        /// Initializes a counter for one thread.
        /// </summary>
        /// <param name="threadId">Managed thread whose exceptions are counted.</param>
        public FirstChanceExceptionCounter(int threadId) {
            ThreadId = threadId;
        }

        /// <summary>Gets the number of first-chance exceptions seen on the counted thread.</summary>
        public int Count { get; private set; }

        /// <summary>
        /// <see cref="AppDomain.FirstChanceException"/> handler: counts the exception when it was raised on the counted thread.
        /// </summary>
        /// <param name="sender">Event source.</param>
        /// <param name="e">Exception details.</param>
        public void Record(object sender, FirstChanceExceptionEventArgs e) {
            if (Environment.CurrentManagedThreadId == ThreadId) {
                Count++;
            }
        }
    }
}
