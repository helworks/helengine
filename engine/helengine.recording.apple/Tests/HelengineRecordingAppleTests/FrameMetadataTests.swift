import CoreGraphics
import Foundation
import ScreenCaptureKit
import XCTest
@testable import HelengineRecordingApple

/// Verifies SCK metadata shapes independently of capture permission.
final class FrameMetadataTests: XCTestCase {
    /// CoreGraphics serializes rectangles as dictionaries in native frame attachments.
    func testAcceptsSDKDictionaryRepresentation() throws {
        let bounds = CGRect(x: 30, y: 40, width: 640, height: 392)
        let dictionary: CFDictionary = bounds.dictionaryRepresentation
        XCTAssertEqual(try FrameMetadata.windowBounds([.screenRect: dictionary]), bounds)
    }

    /// Missing or malformed original geometry must fail rather than use unrelated screen bounds.
    func testRejectsMissingAndMalformedBounds() {
        XCTAssertThrowsError(try FrameMetadata.windowBounds([:]))
        XCTAssertThrowsError(try FrameMetadata.windowBounds([.screenRect: ["Width": 640]]))
    }
}
