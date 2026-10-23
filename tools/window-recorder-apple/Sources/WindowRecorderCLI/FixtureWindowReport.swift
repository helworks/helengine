/// Identifies only the recorder's own synthetic integration-test window.
struct FixtureWindowReport: Codable {
    /// Exact compositor window ID suitable for the recorder command.
    let windowID: UInt32
    /// Owning process, so callers cannot accidentally capture another window.
    let ownerPID: Int32
    /// Recognizable title for diagnostics, never used for implicit selection.
    let title: String
}
