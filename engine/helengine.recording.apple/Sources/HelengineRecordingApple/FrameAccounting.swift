import Foundation

/// Counts real source updates, overwritten updates, and explicit CFR repeats separately.
struct FrameAccounting {
    /// Counts complete source frames received.
    private(set) var sourceFrames = 0
    /// Counts complete updates superseded before an output slot consumed them.
    private(set) var droppedSourceFrames = 0
    /// Counts output slots reusing the previously consumed source frame.
    private(set) var duplicateFrames = 0
    /// Stores when the latest source frame was observed on the monotonic timeline.
    private var latestObservedSeconds: Double = 0
    /// Retains the first or previously selected observation when the newest update is in the future.
    private var fallbackSequence = 0
    /// Records eligibility of that retained fallback observation.
    private var fallbackObservedSeconds: Double = 0
    /// Identifies the source frame used by the most recent encoded slot.
    private var consumedSequence = 0

    /// Records one complete source update while retaining only its latest pixel buffer elsewhere.
    mutating func receiveSourceFrame(observedSeconds: Double = 0) {
        if sourceFrames > consumedSequence && sourceFrames != fallbackSequence { droppedSourceFrames += 1 }
        sourceFrames += 1
        latestObservedSeconds = observedSeconds
        if fallbackSequence == 0 {
            fallbackSequence = sourceFrames
            fallbackObservedSeconds = observedSeconds
        }
    }

    /// Records one output slot, rejecting use before the first real source frame.
    @discardableResult mutating func consumeSlot(slotSeconds: Double = .greatestFiniteMagnitude) throws -> Int {
        guard sourceFrames > 0 else {
            throw RecordingErrorInfo(code: "first_frame_missing", message: "No real source frame is available for this CFR slot.")
        }
        let latestEligible = latestObservedSeconds <= slotSeconds + 0.000000001
        let selected = latestEligible ? sourceFrames : fallbackSequence
        guard selected > 0, latestEligible || fallbackObservedSeconds <= slotSeconds + 0.000000001 else {
            throw RecordingErrorInfo(code: "eligible_frame_missing", message: "The latest source observation is later than this CFR slot.")
        }
        if consumedSequence == selected { duplicateFrames += 1 }
        consumedSequence = selected
        if latestEligible {
            fallbackSequence = selected
            fallbackObservedSeconds = latestObservedSeconds
        }
        return consumedSequence
    }
}
