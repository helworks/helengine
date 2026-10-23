import Foundation

/// Assigns consecutive 60/1 slots independently of source-frame delivery.
struct FrameTimeline {
    /// Counts the requested output frames, rounding only the final duration upward.
    let targetFrames: Int
    /// Identifies the next CFR slot to encode.
    private(set) var nextSlot = 0

    /// Creates a bounded integer timeline from validated seconds.
    init(durationSeconds: Double) {
        targetFrames = Int(ceil(durationSeconds * 60))
    }

    /// Returns the due slots and rejects more than 250ms of scheduler delay.
    mutating func takeDueSlots(elapsedSeconds: Double) throws -> Range<Int> {
        guard elapsedSeconds.isFinite, elapsedSeconds >= 0 else {
            throw RecordingErrorInfo(code: "invalid_clock", message: "The recording clock must be finite and monotonic.")
        }
        guard nextSlot < targetFrames else { return nextSlot..<nextSlot }
        let deadline = Double(nextSlot) / 60
        guard elapsedSeconds - deadline <= 0.250 + 0.000000001 else {
            throw RecordingErrorInfo(code: "scheduler_lag", message: "The recorder missed its next CFR slot by more than 250ms.")
        }
        let upperBound = min(targetFrames, Int(floor(elapsedSeconds * 60 + 0.0000001)) + 1)
        let slots = nextSlot..<max(nextSlot, upperBound)
        nextSlot = slots.upperBound
        return slots
    }
}
