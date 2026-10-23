import Foundation

/// Throws the original operational failure while retaining a Codable receipt.
public struct RecordingFailure: Error, LocalizedError {
    /// Contains output-prefix status and actual statistics even when recording fails.
    public let result: RecordingResult
    /// Exposes the receipt's explanation through standard Swift error handling.
    public var errorDescription: String? { result.error?.message }
    /// Wraps the receipt without losing encoder or capture diagnostics.
    public init(result: RecordingResult) { self.result = result }
}
