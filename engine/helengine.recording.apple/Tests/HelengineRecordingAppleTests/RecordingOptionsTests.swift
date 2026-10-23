import Foundation
import XCTest
@testable import HelengineRecordingApple

/// Exercises validation without screen access or writing any output.
final class RecordingOptionsTests: XCTestCase {
    /// Checks the agreed 30s, 40Mbps/60Mbps defaults.
    func testDefaults() throws {
        let options = RecordingOptions(windowID: 7, outputURL: URL(fileURLWithPath: "/private/tmp/helengine/macos/recording/menu.mp4"))
        XCTAssertEqual(options.durationSeconds, 30)
        XCTAssertEqual(options.bitrate, 40_000_000)
        XCTAssertEqual(options.maxBitrate, 60_000_000)
        XCTAssertNoThrow(try options.validate())
    }

    /// Rejects invalid IDs, non-MP4 URLs, nonfinite duration, and inverted bitrate limits.
    func testRejectsInvalidOptions() {
        let valid = URL(fileURLWithPath: "/private/tmp/helengine/macos/recording/menu.mp4")
        let options = [
            RecordingOptions(windowID: 0, outputURL: valid),
            RecordingOptions(windowID: 7, outputURL: URL(string: "https://example.org/menu.mp4")!),
            RecordingOptions(windowID: 7, outputURL: URL(fileURLWithPath: "/private/tmp/menu.png")),
            RecordingOptions(windowID: 7, outputURL: valid, durationSeconds: .nan),
            RecordingOptions(windowID: 7, outputURL: valid, durationSeconds: 0),
            RecordingOptions(windowID: 7, outputURL: valid, bitrate: 0),
            RecordingOptions(windowID: 7, outputURL: valid, bitrate: 60_000_001, maxBitrate: 60_000_000)
        ]
        for option in options { XCTAssertThrowsError(try option.validate()) }
    }
}
