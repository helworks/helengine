import Foundation

/// Identifies one capturable application window without recording any pixels.
public struct WindowInfo: Codable {
    /// Specifies the exact ScreenCaptureKit window identifier.
    public let windowID: UInt32
    /// Identifies the owning process, allowing clients to filter their own app.
    public let ownerPID: Int32
    /// Contains the title reported by the window owner.
    public let title: String
    /// Reports native capture width in pixels.
    public let width: Int
    /// Reports native capture height in pixels.
    public let height: Int
}
