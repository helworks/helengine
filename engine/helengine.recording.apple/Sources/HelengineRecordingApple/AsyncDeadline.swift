import Foundation

/// Bounds non-cooperative SDK completion handlers without awaiting a cancelled child task.
enum AsyncDeadline {
    /// Returns one result by the deadline and keeps late callback context valid until the SDK releases it.
    static func run<Value>(seconds: Double, timeoutCode: String,
        onLateCompletion: ((Result<Value, Error>) -> Void)? = nil,
        operation: @escaping (@escaping (Result<Value, Error>) -> Void) -> Void) async throws -> Value {
        try await withCheckedThrowingContinuation { continuation in
            let gate = DeadlineContinuation<Value>(continuation)
            let deadline = DispatchWorkItem {
                gate.resolve(.failure(RecordingErrorInfo(code: timeoutCode, message: "Native operation did not complete within \(seconds) seconds; its callback context remains retained.")))
            }
            DispatchQueue.global(qos: .userInitiated).asyncAfter(deadline: .now() + seconds, execute: deadline)
            operation { result in
                if gate.resolve(result) { deadline.cancel() }
                else { onLateCompletion?(result) }
            }
        }
    }
}
