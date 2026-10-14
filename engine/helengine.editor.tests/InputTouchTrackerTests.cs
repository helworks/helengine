using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>Verifies stable IDs, lossless transitions, buffer lifetimes and the portable touch API.</summary>
    public sealed class InputTouchTrackerTests {
        /// <summary>A tap and reuse of its native ID within one frame retain their separate ordered lifetimes.</summary>
        [Fact]
        public void CaptureFrame_PreservesFastTapsAndIdReuse() {
            using InputTouchTracker tracker = new InputTouchTracker();
            tracker.BeginTouch(7, 10, 20, 0.5f);
            tracker.EndTouch(7, 11, 21, 0f);
            tracker.BeginTouch(7, 100, 200, 1f);
            InputFrameState frame = tracker.CaptureFrame();
            Assert.Equal(3, frame.TouchCount);
            Assert.Equal(InputTouchPhase.Began, frame.Touches[0].Phase);
            Assert.Equal(InputTouchPhase.Ended, frame.Touches[1].Phase);
            Assert.Equal(1, frame.Touches[1].DeltaX);
            Assert.Equal(InputTouchPhase.Began, frame.Touches[2].Phase);
            Assert.Equal(100, frame.Touches[2].X);
            Assert.Equal(1, tracker.ActiveTouchCount);
        }

        /// <summary>Lifting the first finger leaves the other finger's ID and pressure intact even when event indices change.</summary>
        [Fact]
        public void CaptureFrame_TracksIndependentContactsAndPreservesPreviousBuffer() {
            using InputTouchTracker tracker = new InputTouchTracker();
            tracker.BeginTouch(3, 10, 20, 0.5f);
            tracker.BeginTouch(29, 100, 200, 0.75f);
            InputFrameState previous = tracker.CaptureFrame();
            tracker.MoveTouch(29, 115, 190, 0.8f);
            tracker.EndTouch(3, 10, 20, 0f);
            InputFrameState current = tracker.CaptureFrame();
            Assert.NotSame(previous.Touches, current.Touches);
            Assert.Equal(InputTouchPhase.Began, previous.Touches[1].Phase);
            Assert.Equal(100, previous.Touches[1].X);
            Assert.Equal(29, current.Touches[0].Id);
            Assert.Equal(15, current.Touches[0].DeltaX);
            Assert.Equal(-10, current.Touches[0].DeltaY);
            Assert.Equal(InputTouchPhase.Ended, current.Touches[1].Phase);
            InputFrameState held = tracker.CaptureFrame();
            Assert.Equal(1, held.TouchCount);
            Assert.Equal(29, held.Touches[0].Id);
            Assert.Equal(InputTouchPhase.Stationary, held.Touches[0].Phase);
            Assert.Equal(0, held.Touches[0].DeltaX);
            Assert.Equal(0.8f, held.Touches[0].Pressure);
        }

        /// <summary>Large batches of movement grow storage instead of dropping a lift or an entire quick tap.</summary>
        [Fact]
        public void CaptureFrame_GrowsWithoutLosingTransitions() {
            using InputTouchTracker tracker = new InputTouchTracker();
            tracker.BeginTouch(0, 0, 0, 1f);
            for (int index = 1; index <= 100; index++) { tracker.MoveTouch(0, index, 0, 1f); }
            tracker.EndTouch(0, 101, 0, 0f);
            InputFrameState frame = tracker.CaptureFrame();
            Assert.Equal(102, frame.TouchCount);
            Assert.Equal(InputTouchPhase.Ended, frame.Touches[101].Phase);
            Assert.Equal(101, frame.Touches[101].X);
            Assert.Equal(0, tracker.CaptureFrame().TouchCount);
        }

        /// <summary>Focus loss cancels all held fingers and subsequent stale moves cannot resurrect them.</summary>
        [Fact]
        public void CancelAll_AbandonsContactsWithoutSynthesizingLifts() {
            using InputTouchTracker tracker = new InputTouchTracker();
            tracker.BeginTouch(0, 10, 20, 1f);
            tracker.BeginTouch(1, 30, 40, 1f);
            tracker.CaptureFrame();
            tracker.CancelAll();
            tracker.MoveTouch(0, 100, 100, 1f);
            tracker.EndTouch(1, 30, 40, 0f);
            InputFrameState canceled = tracker.CaptureFrame();
            Assert.Equal(2, canceled.TouchCount);
            Assert.Equal(InputTouchPhase.Canceled, canceled.Touches[0].Phase);
            Assert.Equal(InputTouchPhase.Canceled, canceled.Touches[1].Phase);
            Assert.Equal(0, tracker.ActiveTouchCount);
            Assert.Equal(0, tracker.CaptureFrame().TouchCount);
        }

        /// <summary>Uncaptured taps cannot turn into a delayed button click after the host loses focus and resumes.</summary>
        [Fact]
        public void CancelAll_DiscardsUndeliveredTaps() {
            using InputTouchTracker tracker = new InputTouchTracker();
            tracker.BeginTouch(0, 10, 20, 1f);
            tracker.EndTouch(0, 10, 20, 0f);
            tracker.CancelAll();
            Assert.Equal(0, tracker.CaptureFrame().TouchCount);
            tracker.BeginTouch(1, 30, 40, 1f);
            tracker.CancelAll();
            InputFrameState canceled = tracker.CaptureFrame();
            Assert.Equal(1, canceled.TouchCount);
            Assert.Equal(InputTouchPhase.Canceled, canceled.Touches[0].Phase);
        }

        /// <summary>Invalid IDs, invalid pressure and duplicate active contacts fail before corrupting the contact stream.</summary>
        [Fact]
        public void BeginTouch_RejectsInvalidSamplesAndDuplicateIds() {
            using InputTouchTracker tracker = new InputTouchTracker();
            Assert.Throws<ArgumentOutOfRangeException>(() => tracker.BeginTouch(-1, 0, 0, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => tracker.BeginTouch(0, 0, 0, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => tracker.BeginTouch(0, 0, 0, -1f));
            tracker.BeginTouch(0, 0, 0, 1.2f);
            Assert.Throws<ArgumentException>(() => tracker.BeginTouch(0, 100, 100, 1f));
            Assert.Equal(1, tracker.ActiveTouchCount);
        }

        /// <summary>Warmed held and moving contacts reuse their frame buffers without allocating every update.</summary>
        [Fact]
        public void CaptureFrame_ReusesStorageAfterWarmup() {
            using InputTouchTracker tracker = new InputTouchTracker();
            tracker.BeginTouch(0, 0, 0, 1f);
            tracker.CaptureFrame();
            tracker.MoveTouch(0, 1, 0, 1f);
            tracker.CaptureFrame();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 2; index < 202; index++) {
                tracker.MoveTouch(0, index, 0, 1f);
                tracker.CaptureFrame();
            }
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        /// <summary>The engine exposes all contacts rather than interpreting a native pointer index as a stable ID.</summary>
        [Fact]
        public void InputSystem_ExposesOrderedContactsAndChecksSampleIndices() {
            using TestTouchInputBackend backend = new TestTouchInputBackend();
            InputSystem input = new InputSystem();
            input.SetBackend(backend);
            backend.Touches.BeginTouch(15, 20, 30, 1f);
            backend.Touches.BeginTouch(2, 40, 50, 1f);
            input.EarlyUpdate();
            Assert.Equal(2, input.TouchCount);
            Assert.Equal(15, input.GetTouch(0).Id);
            Assert.Equal(2, input.GetTouch(1).Id);
            Assert.Throws<ArgumentOutOfRangeException>(() => input.GetTouch(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => input.GetTouch(2));
        }
    }
}
