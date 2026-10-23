import Foundation

/// Describes an explicit operational failure in the recording receipt.
public struct RecordingErrorInfo: Codable, Error {
    /// Provides a stable machine-readable cause.
    public let code: String
    /// Explains the failed operation and its observed result.
    public let message: String

    /// Creates one failure without changing any recording state.
    public init(code: String, message: String) {
        self.code = code
        self.message = message
    }
}
