import Foundation

/// Rejects resizing or changing the captured window's native-pixel geometry.
struct CaptureGeometry {
    /// Stores the initial native-pixel width.
    let width: Int
    /// Stores the initial native-pixel height.
    let height: Int
    /// Stores the selected window's original point width.
    let windowWidth: Double
    /// Stores the selected window's original point height.
    let windowHeight: Double

    /// Rejects changing pixel or point dimensions instead of stretching the recording.
    func validate(pixelWidth: Int, pixelHeight: Int, observedWindowWidth: Double, observedWindowHeight: Double) throws {
        guard pixelWidth == width, pixelHeight == height,
              observedWindowWidth.isFinite, observedWindowHeight.isFinite,
              abs(observedWindowWidth - windowWidth) <= 0.5,
              abs(observedWindowHeight - windowHeight) <= 0.5 else {
            throw RecordingErrorInfo(code: "window_resized", message: "Window bounds changed; the recorder will not stretch, pad, or upscale its fixed native-pixel stream.")
        }
    }
}
