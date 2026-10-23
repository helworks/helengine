import XCTest
@testable import RecorderCommand

/// Pins user input before any permission request, capture, or encoder allocation.
final class RecorderCommandTests: XCTestCase {
    /// An exact window and absolute destination are mandatory for recording.
    func testRecordDefaultsAndExplicitWindow() throws {
        let command = try RecorderCommand.parse(["record", "--window-id", "42", "--output", "/private/tmp/helengine/macos/menu.mp4"])
        XCTAssertEqual(command.windowID, 42)
        XCTAssertEqual(command.durationSeconds, 30)
        XCTAssertEqual(command.bitrate, 40_000_000)
        XCTAssertEqual(command.maxBitrate, 60_000_000)
    }
    /// Requested high bitrate values survive unit parsing without changing FPS.
    func testExplicitRecordingOptions() throws {
        let command = try RecorderCommand.parse(["record", "--window-id", "12", "--output", "/private/tmp/menu.mp4", "--duration", "2.5", "--fps", "60", "--bitrate", "50M", "--max-bitrate", "80M", "--result", "/private/tmp/result.json"])
        XCTAssertEqual(command.durationSeconds, 2.5)
        XCTAssertEqual(command.bitrate, 50_000_000)
        XCTAssertEqual(command.maxBitrate, 80_000_000)
        XCTAssertEqual(command.resultPath, "/private/tmp/result.json")
    }
    /// A malformed command must never select another window or substitute defaults.
    func testInvalidCommandsAreRejected() {
        let commands = [
            [], ["unknown"], ["record"],
            ["record", "--window-id", "0", "--output", "/private/tmp/menu.mp4"],
            ["record", "--window-id", "4294967296", "--output", "/private/tmp/menu.mp4"],
            ["record", "--window-id", "1", "--output", "relative.mp4"],
            ["record", "--window-id", "1", "--output", "/private/tmp/menu.mov"],
            ["record", "--window-id", "1", "--output", "/private/tmp/menu.mp4", "--fps", "30"],
            ["record", "--window-id", "1", "--output", "/private/tmp/menu.mp4", "--duration", "nan"],
            ["record", "--window-id", "1", "--output", "/private/tmp/menu.mp4", "--duration", "0"],
            ["record", "--window-id", "1", "--output", "/private/tmp/menu.mp4", "--bitrate", "70M"],
            ["record", "--window-id", "1", "--output", "/private/tmp/menu.mp4", "--bitrate", "9223372036854775807M"],
            ["record", "--window-id", "1", "--output", "/private/tmp/menu.mp4", "--window-id", "2"],
            ["windows", "--output", "/private/tmp/menu.mp4"],
            ["permission", "--result", "relative.json"],
            ["record", "--request"]
        ]
        for command in commands { XCTAssertThrowsError(try RecorderCommand.parse(command), "Accepted invalid arguments: \(command)") }
    }
    /// Permission is requested only by an explicit request flag on that command.
    func testPermissionAndWindowListing() throws {
        XCTAssertFalse(try RecorderCommand.parse(["permission"]).requestPermission)
        XCTAssertTrue(try RecorderCommand.parse(["permission", "--request"]).requestPermission)
        XCTAssertEqual(try RecorderCommand.parse(["windows"]).action, .windows)
        XCTAssertEqual(try RecorderCommand.parse(["fixture", "--result", "/private/tmp/fixture.json"]).action, .fixture)
    }
}
