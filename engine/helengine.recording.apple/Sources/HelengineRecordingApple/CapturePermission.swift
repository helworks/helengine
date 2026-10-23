import CoreGraphics

/// Exposes the caller's existing Screen Recording grant without changing another app's identity.
public enum CapturePermission {
    /// Checks permission without showing a request or capturing any content.
    public static func isGranted() -> Bool { CGPreflightScreenCaptureAccess() }
    /// Requests permission only when a client explicitly invokes this operation.
    public static func request() -> Bool { CGRequestScreenCaptureAccess() }
}
