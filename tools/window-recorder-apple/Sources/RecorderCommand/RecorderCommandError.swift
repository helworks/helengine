import Foundation

/// Preserves the invalid input explanation without substituting another command.
public enum RecorderCommandError: LocalizedError {
    case invalid(String)
    /// User-facing validation failure suitable for the JSON receipt.
    public var errorDescription: String? {
        switch self { case .invalid(let message): return message }
    }
}
