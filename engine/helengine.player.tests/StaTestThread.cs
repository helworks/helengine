namespace helengine.player.tests {
    /// <summary>
    /// Runs a test body on a dedicated single-threaded-apartment thread, which WinForms windows require, and rethrows
    /// nothing itself: the failure is surfaced as an xUnit assertion on the calling test thread.
    /// </summary>
    public static class StaTestThread {
        /// <summary>
        /// Runs <paramref name="check"/> on a new STA thread, waits for it and fails the test with its exception, if any.
        /// </summary>
        /// <param name="check">Test body to run.</param>
        public static void Run(Action check) {
            if (check == null) {
                throw new ArgumentNullException(nameof(check));
            }

            Exception failure = null;
            Thread thread = new Thread(() => {
                try {
                    check();
                } catch (Exception exception) {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }
    }
}
