import AppKit
import Foundation
import RecorderCommand
import HelengineRecordingApple

/// Runs a single explicit operation; reusable capture and codec ownership stay in the library.
@main
struct RecorderMain {
    /// Emits exactly one JSON receipt, including a native failure with any playable prefix.
    @MainActor
    static func main() async {
        var resultPath: String?
        do {
            let command = try RecorderCommand.parse(Array(CommandLine.arguments.dropFirst()))
            resultPath = command.resultPath
            switch command.action {
            case .windows:
                let windows = try await WindowCatalog.list()
                try RecorderJSON.write(windows, path: resultPath)
            case .permission:
                let granted = command.requestPermission ? CapturePermission.request() : CapturePermission.isGranted()
                try RecorderJSON.write(RecorderPermissionReport(granted: granted, requested: command.requestPermission), path: resultPath)
                if !granted { exit(3) }
            case .record:
                guard CapturePermission.isGranted() else {
                    try RecorderJSON.write(RecorderFailureReport(code: "permission_required", message: "Allow Helengine Recorder in System Settings > Privacy & Security > Screen & System Audio Recording, then relaunch this application."), path: resultPath)
                    exit(3)
                }
                guard let outputPath = command.outputPath else { throw RecorderCommandError.invalid("An explicit output path is required.") }
                let options = RecordingOptions(windowID: command.windowID, outputURL: URL(fileURLWithPath: outputPath), durationSeconds: command.durationSeconds, bitrate: command.bitrate, maxBitrate: command.maxBitrate)
                try options.validate()
                try RecorderJSON.writeStarted(RecorderStartReport(hostPID: ProcessInfo.processInfo.processIdentifier, windowID: command.windowID, outputPath: outputPath), path: resultPath)
                let result = try await WindowRecorder().record(options: options)
                try RecorderJSON.write(result, path: resultPath)
                if !result.success { exit(3) }
            case .fixture:
                let fixture = FixtureApplication()
                try withExtendedLifetime(fixture) { try fixture.run(resultPath: resultPath) }
            }
        } catch let failure as RecordingFailure {
            do { try RecorderJSON.write(failure.result, path: resultPath) }
            catch { fputs("Failed to write the recording receipt: \(error.localizedDescription)\n", stderr) }
            fputs("\(failure.localizedDescription)\n", stderr)
            exit(3)
        } catch {
            do { try RecorderJSON.write(RecorderFailureReport(code: "operation_failed", message: error.localizedDescription), path: resultPath) }
            catch { fputs("Failed to write the operation receipt: \(error.localizedDescription)\n", stderr) }
            fputs("\(error.localizedDescription)\n", stderr)
            exit(error is RecorderCommandError ? 2 : 3)
        }
    }
}
