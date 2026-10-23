import Foundation

/// Records one exact application window to silent hardware-HEVC MP4 at 60/1 CFR.
public final class WindowRecorder {
    /// Creates a recorder without requesting permission or allocating capture resources.
    public init() {}

    /// Records the selected window or throws a receipt containing any preserved prefix.
    public func record(options: RecordingOptions) async throws -> RecordingResult {
        do {
            try options.validate()
            try Task.checkCancellation()
            let window = try await WindowCatalog.find(windowID: options.windowID)
            let session = try CaptureSession(options: options, window: window)
            return try await session.run()
        } catch let failure as RecordingFailure {
            throw failure
        } catch {
            let issue: RecordingErrorInfo
            if let known = error as? RecordingErrorInfo { issue = known }
            else if error is CancellationError { issue = RecordingErrorInfo(code: "cancelled", message: "Recording was cancelled before capture.") }
            else { issue = RecordingErrorInfo(code: "recording_failed", message: String(describing: error)) }
            throw RecordingFailure(result: RecordingResult(success: false, outputPath: options.outputURL.path,
                prefixPreserved: false, error: issue, stats: RecordingStatistics(options: options)))
        }
    }
}
