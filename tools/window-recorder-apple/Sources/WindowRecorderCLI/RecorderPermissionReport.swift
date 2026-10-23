/// Reports permission state without triggering a capture.
struct RecorderPermissionReport: Codable {
    /// Whether screen capture is currently authorized for this application.
    let granted: Bool
    /// Distinguishes an explicit request from a metadata-only query.
    let requested: Bool
}
