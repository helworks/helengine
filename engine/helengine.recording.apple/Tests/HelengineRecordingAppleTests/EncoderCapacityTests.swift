import Dispatch
import Foundation
import XCTest
@testable import HelengineRecordingApple

/// Checks bounded waiting and failure delivery without relying on hardware callback timing.
final class EncoderCapacityTests: XCTestCase {
    /// Four acquired slots exclude a fifth and a monotonic timeout leaves the capacity unchanged.
    func testFourSlotsTimeoutWithoutCreatingAFifthSlot() throws {
        let capacity = EncoderCapacity()
        for _ in 0..<4 { try capacity.acquire(deadline: .now() + .seconds(1)) }
        defer { for _ in 0..<4 { capacity.release() } }
        let start = DispatchTime.now().uptimeNanoseconds
        XCTAssertThrowsError(try capacity.acquire(deadline: .now() + .milliseconds(20))) {
            XCTAssertEqual(($0 as? RecordingErrorInfo)?.code, "encoder_backpressure_timeout")
        }
        XCTAssertGreaterThanOrEqual(DispatchTime.now().uptimeNanoseconds - start, 15_000_000)
        capacity.release()
        try capacity.acquire(deadline: .now() + .seconds(1))
        XCTAssertThrowsError(try capacity.acquire(deadline: .now()))
    }

    /// An already-expired batch rejects submission even with free capacity and leaves every permit available.
    func testExpiredDeadlineRejectsFreePermitWithoutConsumingCapacity() throws {
        let capacity = EncoderCapacity()
        do {
            try capacity.acquire(deadline: .now() - .milliseconds(1))
            capacity.release()
            XCTFail("An expired deadline accepted a submission with free capacity.")
        } catch {
            XCTAssertEqual((error as? RecordingErrorInfo)?.code, "encoder_backpressure_timeout")
        }
        for _ in 0..<4 { try capacity.acquire(deadline: .now() + .seconds(1)) }
        defer { for _ in 0..<4 { capacity.release() } }
        XCTAssertThrowsError(try capacity.acquire(deadline: .now()))
    }

    /// Reusing one batch deadline never grants another full wait interval to a later submission.
    func testTwoWaitsShareOneDeadlineWithoutRenewal() throws {
        let capacity = EncoderCapacity()
        for _ in 0..<4 { try capacity.acquire(deadline: .now() + .seconds(1)) }
        defer { for _ in 0..<4 { capacity.release() } }
        let deadline = DispatchTime.now() + .milliseconds(40)
        XCTAssertThrowsError(try capacity.acquire(deadline: deadline)) {
            XCTAssertEqual(($0 as? RecordingErrorInfo)?.code, "encoder_backpressure_timeout")
        }
        let secondStart = DispatchTime.now().uptimeNanoseconds
        XCTAssertThrowsError(try capacity.acquire(deadline: deadline)) {
            XCTAssertEqual(($0 as? RecordingErrorInfo)?.code, "encoder_backpressure_timeout")
        }
        XCTAssertLessThan(DispatchTime.now().uptimeNanoseconds - secondStart, 20_000_000)
    }

    /// A native completion wakes a blocked submission without polling or increasing the four-slot bound.
    func testCallbackReleaseWakesWaitingSubmission() throws {
        let capacity = EncoderCapacity()
        for _ in 0..<4 { try capacity.acquire(deadline: .now() + .seconds(1)) }
        defer { for _ in 0..<4 { capacity.release() } }
        DispatchQueue.global().asyncAfter(deadline: .now() + .milliseconds(20)) { capacity.release() }
        try capacity.acquire(deadline: .now() + .seconds(1))
        XCTAssertThrowsError(try capacity.acquire(deadline: .now()))
    }

    /// A failed callback releases its slot and delivers the original failure to waiting and subsequent submissions.
    func testCallbackErrorWakesWaiterAndPreservesOriginalFailure() throws {
        let capacity = EncoderCapacity()
        for _ in 0..<4 { try capacity.acquire(deadline: .now() + .seconds(1)) }
        defer { for _ in 0..<3 { capacity.release() } }
        let error = RecordingErrorInfo(code: "encoder_callback_failed", message: "Synthetic callback failure")
        DispatchQueue.global().asyncAfter(deadline: .now() + .milliseconds(20)) { capacity.release(failure: error) }
        XCTAssertThrowsError(try capacity.acquire(deadline: .now() + .seconds(1))) {
            XCTAssertEqual(($0 as? RecordingErrorInfo)?.code, error.code)
        }
        XCTAssertThrowsError(try capacity.acquire(deadline: .now())) {
            XCTAssertEqual(($0 as? RecordingErrorInfo)?.message, error.message)
        }
    }
}
