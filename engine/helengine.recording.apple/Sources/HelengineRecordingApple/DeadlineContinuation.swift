import Foundation

/// Completes one checked continuation exactly once, including callbacks arriving after a deadline.
final class DeadlineContinuation<Value>: @unchecked Sendable {
    /// Protects ownership of the single continuation.
    private let lock = NSLock()
    /// Is cleared before resuming so simultaneous native callbacks cannot resume twice.
    private var continuation: CheckedContinuation<Value, Error>?

    /// Takes ownership until either the operation or its deadline wins.
    init(_ continuation: CheckedContinuation<Value, Error>) { self.continuation = continuation }

    /// Returns whether this result won; late callbacks safely retain only this inert gate.
    @discardableResult func resolve(_ result: Result<Value, Error>) -> Bool {
        lock.lock()
        let pending = continuation
        continuation = nil
        lock.unlock()
        guard let pending else { return false }
        pending.resume(with: result)
        return true
    }
}
