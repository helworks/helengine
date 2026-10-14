namespace helengine.player.tests {
    /// <summary>
    /// Verifies the real main-window lookup reports a process that is not running as "no window" without raising any
    /// exception, so Helena's first-chance-exception debugging sees only the locator's deliberate error.
    /// </summary>
    public sealed class Win32ProcessMainWindowFinderTests {
        /// <summary>
        /// Process id that cannot belong to a running process: Windows process ids are multiples of four, so an odd id
        /// never exists.
        /// </summary>
        const int NonexistentProcessId = int.MaxValue;

        /// <summary>A process that is not running has no main window, and looking it up raises no first-chance exception.</summary>
        [Fact]
        public void Find_WhenProcessNotRunning_ReturnsZeroWithoutFirstChanceExceptions() {
            FirstChanceExceptionCounter counter = new FirstChanceExceptionCounter(Environment.CurrentManagedThreadId);
            AppDomain.CurrentDomain.FirstChanceException += counter.Record;
            IntPtr window;
            try {
                window = Win32ProcessMainWindowFinder.Find(NonexistentProcessId);
            } finally {
                AppDomain.CurrentDomain.FirstChanceException -= counter.Record;
            }

            Assert.Equal(IntPtr.Zero, window);
            Assert.Equal(0, counter.Count);
        }

        /// <summary>A non-positive process id is an invalid input.</summary>
        [Fact]
        public void Find_WhenProcessIdNotPositive_Throws() {
            Assert.Throws<ArgumentOutOfRangeException>(() => Win32ProcessMainWindowFinder.Find(0));
        }
    }
}
