import Foundation

/// Publishes ownership before a potentially slow native call so automation can stop only this recorder process.
struct RecorderStartReport: Encodable {
    /// This intermediate state is never a completed recording receipt.
    let phase = "started"
    /// Identifies the exact application instance owned by this invocation.
    let hostPID: Int32
    /// Identifies the sole selected source window.
    let windowID: UInt32
    /// Names the requested destination before any publication.
    let outputPath: String
}
