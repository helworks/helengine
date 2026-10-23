import AppKit
import Foundation
import ScreenCaptureKit

/// Lists application windows and resolves exact identifiers without desktop fallback.
public enum WindowCatalog {
    /// Returns windows with owning PIDs and native-pixel bounds; takes no screenshot.
    @MainActor public static func list() async throws -> [WindowInfo] {
        let windows = try await shareableWindows()
        return windows.compactMap { window in
            guard let owner = window.owningApplication else { return nil }
            let filter = SCContentFilter(desktopIndependentWindow: window)
            return WindowInfo(windowID: window.windowID, ownerPID: owner.processID, title: window.title ?? "",
                width: Int(ceil(filter.contentRect.width * Double(filter.pointPixelScale))),
                height: Int(ceil(filter.contentRect.height * Double(filter.pointPixelScale))))
        }.sorted { $0.windowID < $1.windowID }
    }

    /// Resolves only the requested window and never substitutes another source.
    @MainActor static func find(windowID: UInt32) async throws -> SCWindow {
        guard let window = try await shareableWindows().first(where: { $0.windowID == windowID }) else {
            throw RecordingErrorInfo(code: "window_not_found", message: "No visible application window has exact ID \(windowID).")
        }
        return window
    }

    /// Initializes the caller's WindowServer connection and enumerates permitted windows.
    @MainActor static func shareableWindows() async throws -> [SCWindow] {
        guard CapturePermission.isGranted() else {
            throw RecordingErrorInfo(code: "permission_required", message: "Screen Recording permission is required for this recorder app.")
        }
        NSApplication.shared.setActivationPolicy(.accessory)
        let content = try await SCShareableContent.excludingDesktopWindows(true, onScreenWindowsOnly: true)
        return content.windows.filter { $0.windowLayer == 0 && $0.frame.width > 0 && $0.frame.height > 0 && $0.owningApplication != nil }
    }
}
