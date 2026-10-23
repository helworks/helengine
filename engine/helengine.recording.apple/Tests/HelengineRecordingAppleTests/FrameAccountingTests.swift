import XCTest
@testable import HelengineRecordingApple

/// Verifies duplication receipts do not claim source frames that were never rendered.
final class FrameAccountingTests: XCTestCase {
    /// Counts unchanged-window repeats separately from source updates and overwritten updates.
    func testTracksDuplicatesAndSupersededSourceFrames() throws {
        var accounting = FrameAccounting()
        XCTAssertThrowsError(try accounting.consumeSlot())
        accounting.receiveSourceFrame()
        try accounting.consumeSlot()
        try accounting.consumeSlot()
        accounting.receiveSourceFrame()
        accounting.receiveSourceFrame()
        accounting.receiveSourceFrame()
        try accounting.consumeSlot()
        XCTAssertEqual(accounting.sourceFrames, 4)
        XCTAssertEqual(accounting.duplicateFrames, 1)
        XCTAssertEqual(accounting.droppedSourceFrames, 2)
    }
    /// Never inserts a later callback into an earlier slot, even when intermediate updates were overwritten.
    func testBacklogNeverConsumesAFutureSourceFrame() throws {
        var accounting = FrameAccounting()
        accounting.receiveSourceFrame(observedSeconds: 0.016)
        XCTAssertEqual(try accounting.consumeSlot(slotSeconds: 0.016), 1)
        accounting.receiveSourceFrame(observedSeconds: 0.032)
        accounting.receiveSourceFrame(observedSeconds: 0.049)
        XCTAssertEqual(try accounting.consumeSlot(slotSeconds: 0.033), 1)
        XCTAssertEqual(try accounting.consumeSlot(slotSeconds: 0.050), 3)
        XCTAssertEqual(accounting.droppedSourceFrames, 1)
        XCTAssertEqual(accounting.duplicateFrames, 1)
    }

    /// Keeps the first observation available when callbacks arrive before the timer's initial slot.
    func testFirstFrameSurvivesCallbackBacklogBeforeSlotZero() throws {
        var accounting = FrameAccounting()
        accounting.receiveSourceFrame(observedSeconds: 0)
        accounting.receiveSourceFrame(observedSeconds: 0.001)
        XCTAssertEqual(try accounting.consumeSlot(slotSeconds: 0), 1)
        XCTAssertEqual(accounting.droppedSourceFrames, 0)
        XCTAssertEqual(accounting.duplicateFrames, 0)
        XCTAssertEqual(try accounting.consumeSlot(slotSeconds: 1.0 / 60), 2)
        XCTAssertEqual(accounting.droppedSourceFrames, 0)
    }

}
