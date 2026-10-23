import Dispatch
import Foundation

/// Bounds encoder-owned frames and waits for a native callback with a monotonic deadline instead of dropping output.
final class EncoderCapacity: @unchecked Sendable {
    /// Represents the four buffers that the encoder may retain concurrently.
    private let available = DispatchSemaphore(value: 4)
    /// Protects the first asynchronous failure observed by producers and callbacks.
    private let lock = NSLock()
    /// Stores the original callback failure so a waiting submission cannot mask it as a timeout.
    private var firstFailure: RecordingErrorInfo?

    /// Reserves one buffer slot or fails within the caller's bounded monotonic interval.
    func acquire(deadline: DispatchTime) throws {
        if let failure = failure() { throw failure }
        guard available.wait(timeout: deadline) == .success else {
            if let failure = failure() { throw failure }
            throw RecordingErrorInfo(code: "encoder_backpressure_timeout", message: "The hardware encoder did not release one of its four pending frames within the bounded submission deadline.")
        }
        if let failure = failure() {
            available.signal()
            throw failure
        }
    }

    /// Releases exactly one submitted frame after its callback finishes and exposes any original callback failure.
    func release(failure: RecordingErrorInfo? = nil) {
        lock.lock()
        if firstFailure == nil { firstFailure = failure }
        lock.unlock()
        available.signal()
    }

    /// Reads the original callback error under the same lock used by callback completion.
    private func failure() -> RecordingErrorInfo? {
        lock.lock()
        defer { lock.unlock() }
        return firstFailure
    }
}
