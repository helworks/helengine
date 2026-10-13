using System.Diagnostics;
using System.Runtime.InteropServices;

namespace helengine.player {
    /// <summary>
    /// Real player frame clock: a <see cref="Stopwatch"/> for time and <see cref="Thread.Sleep(int)"/> for waits. While
    /// it is alive it holds a 1 ms Windows timer resolution, because the default 15.6 ms granularity would round every
    /// short sleep up past the frame deadline.
    /// </summary>
    public sealed class StopwatchPlayerFrameClock : IPlayerFrameClock, IDisposable {
        /// <summary>
        /// Windows timer resolution, in milliseconds, requested while the clock is alive.
        /// </summary>
        const uint TimerResolutionMilliseconds = 1;

        /// <summary>
        /// Number of spin iterations performed by one <see cref="Spin"/> call.
        /// </summary>
        const int SpinIterations = 32;

        /// <summary>
        /// Monotonic stopwatch started when the clock is created.
        /// </summary>
        readonly Stopwatch Stopwatch;

        /// <summary>
        /// Whether the timer resolution request has already been released.
        /// </summary>
        bool Disposed;

        /// <summary>
        /// Starts the clock and requests the 1 ms system timer resolution.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when Windows refuses the timer resolution request.</exception>
        public StopwatchPlayerFrameClock() {
            uint result = TimeBeginPeriod(TimerResolutionMilliseconds);
            if (result != 0) {
                throw new InvalidOperationException($"timeBeginPeriod({TimerResolutionMilliseconds}) failed with code {result}; the player cannot pace frames precisely.");
            }

            Stopwatch = Stopwatch.StartNew();
        }

        /// <summary>
        /// Gets the seconds elapsed since the clock was created.
        /// </summary>
        public double ElapsedSeconds => Stopwatch.Elapsed.TotalSeconds;

        /// <summary>
        /// Sleeps the calling thread for the requested number of milliseconds.
        /// </summary>
        /// <param name="milliseconds">Positive number of milliseconds to sleep.</param>
        public void Sleep(int milliseconds) {
            Thread.Sleep(milliseconds);
        }

        /// <summary>
        /// Busy-waits for a few processor cycles.
        /// </summary>
        public void Spin() {
            Thread.SpinWait(SpinIterations);
        }

        /// <summary>
        /// Releases the 1 ms timer resolution request.
        /// </summary>
        public void Dispose() {
            if (Disposed) {
                return;
            }

            Disposed = true;
            TimeEndPeriod(TimerResolutionMilliseconds);
        }

        /// <summary>
        /// Requests a minimum system timer resolution so short sleeps become accurate.
        /// </summary>
        /// <param name="uMilliseconds">Requested timer resolution in milliseconds.</param>
        /// <returns>Zero when the request succeeded.</returns>
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        static extern uint TimeBeginPeriod(uint uMilliseconds);

        /// <summary>
        /// Releases a previously requested system timer resolution.
        /// </summary>
        /// <param name="uMilliseconds">Previously requested timer resolution in milliseconds.</param>
        /// <returns>Zero when the release succeeded.</returns>
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        static extern uint TimeEndPeriod(uint uMilliseconds);
    }
}
