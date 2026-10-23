import Foundation

/// Reports command and permission failures before a native recording result exists.
struct RecorderFailureReport: Encodable {
    /// This report can never be mistaken for a successful recording.
    let success = false
    /// Stable stage or operation describing the failure.
    let code: String
    /// Original error description, without substituting another capture mode.
    let message: String
}
