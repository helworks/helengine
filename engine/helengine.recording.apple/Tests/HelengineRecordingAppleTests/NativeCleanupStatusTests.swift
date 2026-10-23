import XCTest
@testable import HelengineRecordingApple

/// Empty output is a failure, but is not evidence of an outstanding SDK operation.
final class NativeCleanupStatusTests: XCTestCase {
    /// A start that completes before successful stop clears startup uncertainty even when there are zero encoded frames.
    func testCompletedNativeCleanupIsNotPendingForEmptyOutput() {
        XCTAssertFalse(NativeCleanupStatus(startCompleted: true, stopPending: false, writerPending: false).isPending)
    }

    /// Genuine native callbacks still outstanding remain explicit.
    func testPendingNativeBoundariesStayPending() {
        XCTAssertTrue(NativeCleanupStatus(startCompleted: false, stopPending: false, writerPending: false).isPending)
        XCTAssertTrue(NativeCleanupStatus(startCompleted: true, stopPending: true, writerPending: false).isPending)
        XCTAssertTrue(NativeCleanupStatus(startCompleted: true, stopPending: false, writerPending: true).isPending)
    }
}
