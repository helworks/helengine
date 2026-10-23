import Foundation
import XCTest
@testable import HelengineRecordingApple

/// Checks public failure receipts without obtaining permission or starting a stream.
final class WindowRecorderTests: XCTestCase {
    /// Validates arguments before any screen or encoder operation.
    func testInvalidWindowIDThrowsAJSONReceipt() async throws {
        do {
            _ = try await WindowRecorder().record(options: RecordingOptions(windowID: 0, outputURL: URL(fileURLWithPath: "/private/tmp/helengine/macos/recording/invalid.mp4")))
            XCTFail("Invalid options must fail.")
        } catch let failure as RecordingFailure {
            XCTAssertFalse(failure.result.success)
            XCTAssertEqual(failure.result.error?.code, "invalid_options")
            XCTAssertEqual(failure.result.stats.encodedFrames, 0)
            XCTAssertFalse(failure.result.prefixPreserved)
        }
    }
}
