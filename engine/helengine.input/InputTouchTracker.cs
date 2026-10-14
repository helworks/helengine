namespace helengine {
    /// <summary>Retains ordered contact transitions between frames, including complete taps drained before capture.</summary>
    public sealed class InputTouchTracker : IDisposable {
        /// <summary>Owns the current sample for each held contact.</summary>
        [NativeOwnedMember] List<InputTouchState> ActiveTouches;
        /// <summary>Owns samples awaiting capture in their original event order.</summary>
        [NativeOwnedMember] List<InputTouchState> PendingSamples;
        /// <summary>Owns the first reusable frame buffer; the preceding frame remains valid until the next capture.</summary>
        [NativeOwnedMember] InputTouchState[] FirstFrame;
        /// <summary>Owns the alternating reusable frame buffer.</summary>
        [NativeOwnedMember] InputTouchState[] SecondFrame;
        /// <summary>Selects the buffer that does not contain the preceding frame.</summary>
        bool UseSecondFrame;

        /// <summary>Creates reusable storage without limiting the number of events captured in one frame.</summary>
        public InputTouchTracker() {
            ActiveTouches = new List<InputTouchState>();
            PendingSamples = new List<InputTouchState>();
            FirstFrame = new InputTouchState[16];
            SecondFrame = new InputTouchState[16];
        }

        /// <summary>Gets the number of fingers still touching the surface.</summary>
        public int ActiveTouchCount => ActiveTouches.Count;

        /// <summary>Begins one contact, rejecting reused IDs until their preceding contact has ended.</summary>
        public void BeginTouch(int id, int x, int y, float pressure) {
            ValidateSample(id, pressure);
            if (FindTouch(id) >= 0) {
                throw new ArgumentException("A touch with this ID is already active.", nameof(id));
            }
            InputTouchState sample = new InputTouchState { Id = id, X = x, Y = y, Pressure = pressure, Phase = InputTouchPhase.Began };
            ActiveTouches.Add(sample);
            PendingSamples.Add(sample);
        }

        /// <summary>Records movement or pressure changes; ignores contacts discarded when the host lost focus.</summary>
        public void MoveTouch(int id, int x, int y, float pressure) {
            ValidateSample(id, pressure);
            int index = FindTouch(id);
            if (index < 0) { return; }
            InputTouchState sample = ActiveTouches[index];
            if (sample.X == x && sample.Y == y && sample.Pressure == pressure) { return; }
            sample.DeltaX = x - sample.X;
            sample.DeltaY = y - sample.Y;
            sample.X = x;
            sample.Y = y;
            sample.Pressure = pressure;
            sample.Phase = InputTouchPhase.Moved;
            ActiveTouches[index] = sample;
            PendingSamples.Add(sample);
        }

        /// <summary>Records a normal lift with its final coordinates, leaving other fingers held.</summary>
        public void EndTouch(int id, int x, int y, float pressure) {
            ValidateSample(id, pressure);
            int index = FindTouch(id);
            if (index < 0) { return; }
            InputTouchState sample = ActiveTouches[index];
            sample.DeltaX = x - sample.X;
            sample.DeltaY = y - sample.Y;
            sample.X = x;
            sample.Y = y;
            sample.Pressure = pressure;
            sample.Phase = InputTouchPhase.Ended;
            ActiveTouches.RemoveAt(index);
            PendingSamples.Add(sample);
        }

        /// <summary>Discards undelivered gestures and publishes cancellation for held fingers, preventing clicks after host interruption.</summary>
        public void CancelAll() {
            PendingSamples.Clear();
            for (int index = 0; index < ActiveTouches.Count; index++) {
                InputTouchState sample = ActiveTouches[index];
                sample.Phase = InputTouchPhase.Canceled;
                sample.DeltaX = 0;
                sample.DeltaY = 0;
                PendingSamples.Add(sample);
            }
            ActiveTouches.Clear();
        }

        /// <summary>Returns ordered pending events and a stationary sample for each otherwise unchanged held finger.</summary>
        public InputFrameState CaptureFrame() {
            int capacity = PendingSamples.Count + ActiveTouches.Count;
            if (UseSecondFrame && SecondFrame.Length < capacity) {
                NativeOwnership.Release(ref SecondFrame);
                SecondFrame = new InputTouchState[capacity * 2];
            } else if (!UseSecondFrame && FirstFrame.Length < capacity) {
                NativeOwnership.Release(ref FirstFrame);
                FirstFrame = new InputTouchState[capacity * 2];
            }
            InputTouchState[] buffer = UseSecondFrame ? SecondFrame : FirstFrame;
            int count = PendingSamples.Count;
            for (int index = 0; index < count; index++) { buffer[index] = PendingSamples[index]; }
            for (int index = 0; index < ActiveTouches.Count; index++) {
                InputTouchState sample = ActiveTouches[index];
                if (sample.Phase == InputTouchPhase.Stationary) { buffer[count++] = sample; }
                sample.Phase = InputTouchPhase.Stationary;
                sample.DeltaX = 0;
                sample.DeltaY = 0;
                ActiveTouches[index] = sample;
            }
            PendingSamples.Clear();
            UseSecondFrame = !UseSecondFrame;
            return new InputFrameState { Touches = buffer, TouchCount = count };
        }

        /// <summary>Releases owned native storage after the backend and its frame borrowers finish using it.</summary>
        public void Dispose() {
            NativeOwnership.Release(ref ActiveTouches);
            NativeOwnership.Release(ref PendingSamples);
            NativeOwnership.Release(ref FirstFrame);
            NativeOwnership.Release(ref SecondFrame);
        }

        /// <summary>Finds an active ID without depending on native event index order.</summary>
        int FindTouch(int id) {
            for (int index = 0; index < ActiveTouches.Count; index++) {
                if (ActiveTouches[index].Id == id) { return index; }
            }
            return -1;
        }

        /// <summary>Rejects invalid platform samples before they enter a captured frame.</summary>
        void ValidateSample(int id, float pressure) {
            if (id < 0) { throw new ArgumentOutOfRangeException(nameof(id)); }
            if (float.IsNaN(pressure) || float.IsInfinity(pressure) || pressure < 0f) {
                throw new ArgumentOutOfRangeException(nameof(pressure));
            }
        }
    }
}
