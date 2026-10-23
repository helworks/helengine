import XCTest
@testable import HelengineRecordingApple

/// Verifies real-time scheduling, duplication slots, and fatal lag independently of capture.
final class FrameTimelineTests: XCTestCase {
    /// Guarantees exactly 1800 slots at 30 seconds with no accumulated fractional drift.
    func testThirtySecondsHasExactly1800ConsecutiveSlots() throws {
        var timeline = FrameTimeline(durationSeconds: 30)
        var slots: [Int] = []
        for frame in 0..<1800 {
            slots.append(contentsOf: try timeline.takeDueSlots(elapsedSeconds: Double(frame) / 60))
        }
        XCTAssertEqual(slots.count, 1800)
        XCTAssertTrue(slots.elementsEqual(0..<1800))
        XCTAssertTrue(try timeline.takeDueSlots(elapsedSeconds: 30).isEmpty)
    }

    /// Makes missed source deliveries repeat output slots rather than alter CFR timestamps.
    func testDueSlotsCatchUpWithAnExplicitBound() throws {
        var timeline = FrameTimeline(durationSeconds: 1)
        XCTAssertEqual(try timeline.takeDueSlots(elapsedSeconds: 0), 0..<1)
        XCTAssertEqual(try timeline.takeDueSlots(elapsedSeconds: 3.0 / 60), 1..<4)
        XCTAssertEqual(try timeline.takeDueSlots(elapsedSeconds: 3.0 / 60), 4..<4)
        XCTAssertThrowsError(try timeline.takeDueSlots(elapsedSeconds: 0.5))
    }
}
