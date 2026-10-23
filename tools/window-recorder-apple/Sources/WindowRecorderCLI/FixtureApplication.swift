import AppKit

/// Hosts an owned animation for integration tests, without activating other applications.
@MainActor
final class FixtureApplication {
    /// Keeps the synthetic window alive for the test duration.
    let window = NSWindow(contentRect: NSRect(x: 80, y: 80, width: 640, height: 360), styleMask: [.titled, .closable], backing: .buffered, defer: false)
    /// Reuses a single drawable view.
    let view = FixtureView()
    /// Periodic invalidation, released on termination with this process.
    var timer: Timer?
    /// Opens only this process's test window and publishes its exact identity.
    func run(resultPath: String?) throws {
        NSApplication.shared.setActivationPolicy(.accessory)
        window.title = "Helengine Recorder Fixture"
        window.isReleasedWhenClosed = false
        window.contentView = view
        window.orderFront(nil)
        try RecorderJSON.write(FixtureWindowReport(windowID: UInt32(window.windowNumber), ownerPID: ProcessInfo.processInfo.processIdentifier, title: window.title), path: resultPath)
        timer = Timer.scheduledTimer(withTimeInterval: 1.0 / 60, repeats: true) { [weak self] _ in
            guard let self = self else { return }
            self.view.frameIndex += 1
            self.view.needsDisplay = true
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 120) { NSApplication.shared.terminate(nil) }
        withExtendedLifetime(self) { NSApplication.shared.run() }
    }
}
