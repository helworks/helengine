import Foundation
import XCTest
@testable import HelengineRecordingApple

/// Exercises never-completing and late SDK callbacks without starting capture.
final class AsyncDeadlineTests: XCTestCase {
    /// A missing SDK completion cannot keep the caller waiting forever; later callbacks remain safe.
    func testNeverCallbackTimesOutAndLateCallbackIsSafe() async throws {
        var late: ((Result<Int, Error>) -> Void)?
        let start = Date()
        do {
            let _: Int = try await AsyncDeadline.run(seconds: 0.02, timeoutCode: "native_timeout") { late = $0 }
            XCTFail("A never-completing operation must time out.")
        } catch let error as RecordingErrorInfo {
            XCTAssertEqual(error.code, "native_timeout")
        }
        XCTAssertLessThan(Date().timeIntervalSince(start), 0.5)
        let completion = try XCTUnwrap(late)
        completion(.success(42))
        completion(.failure(RecordingErrorInfo(code: "late", message: "late SDK callback")))
    }

    /// Multiple native completions cannot resume one continuation twice.
    func testImmediateSuccessWinsExactlyOnce() async throws {
        let value: Int = try await AsyncDeadline.run(seconds: 0.02, timeoutCode: "native_timeout") { done in
            done(.success(7))
            done(.success(8))
        }
        XCTAssertEqual(value, 7)
    }
}
