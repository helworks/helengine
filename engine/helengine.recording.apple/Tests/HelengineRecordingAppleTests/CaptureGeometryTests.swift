import XCTest
@testable import HelengineRecordingApple

/// Protects fixed native-pixel recording against implicit Retina scaling and resize.
final class CaptureGeometryTests: XCTestCase {
    /// Accepts native Retina bounds and rejects both buffer and window resizes.
    func testRejectsResizingEvenWhenSCKKeepsTheOldBufferSize() throws {
        let geometry = CaptureGeometry(width: 128, height: 72, windowWidth: 64, windowHeight: 36)
        XCTAssertNoThrow(try geometry.validate(pixelWidth: 128, pixelHeight: 72, observedWindowWidth: 64, observedWindowHeight: 36))
        XCTAssertThrowsError(try geometry.validate(pixelWidth: 130, pixelHeight: 72, observedWindowWidth: 64, observedWindowHeight: 36))
        XCTAssertThrowsError(try geometry.validate(pixelWidth: 128, pixelHeight: 72, observedWindowWidth: 65, observedWindowHeight: 36))
    }
}
