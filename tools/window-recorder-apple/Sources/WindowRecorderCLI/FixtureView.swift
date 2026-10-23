import AppKit

/// Draws a changing color and moving block for real encoder/capture validation.
final class FixtureView: NSView {
    /// Advances once per timer observation without allocating frame history.
    var frameIndex = 0
    /// Makes the fixture's changing pixels visible to ScreenCaptureKit.
    override func draw(_ dirtyRect: NSRect) {
        NSColor(calibratedHue: CGFloat(frameIndex % 360) / 360, saturation: 0.65, brightness: 0.7, alpha: 1).setFill()
        bounds.fill()
        NSColor.white.setFill()
        NSRect(x: CGFloat((frameIndex * 3) % 560), y: 130, width: 80, height: 80).fill()
        let label = "Helengine recorder fixture ? frame \(frameIndex)"
        label.draw(at: NSPoint(x: 18, y: 24), withAttributes: [.font: NSFont.monospacedSystemFont(ofSize: 18, weight: .bold), .foregroundColor: NSColor.white])
    }
}
