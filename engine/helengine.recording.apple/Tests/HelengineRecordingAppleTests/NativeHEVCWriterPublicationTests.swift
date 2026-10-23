import AVFoundation
import CoreVideo
import Foundation
import XCTest
@testable import HelengineRecordingApple

/// Exercises the real final-publication race after a hardware encoder already owns a partial file.
final class NativeHEVCWriterPublicationTests: XCTestCase {
    /// A concurrently created destination remains intact while a playable encoded prefix is retained.
    func testDestinationCreatedDuringRecordingIsNeverOverwritten() async throws {
        let url = URL(fileURLWithPath: "/private/tmp/helengine/macos/recording/publication-race.mp4")
        let partial = url.deletingPathExtension().appendingPathExtension("partial.mp4")
        XCTAssertFalse(FileManager.default.fileExists(atPath: url.path))
        XCTAssertFalse(FileManager.default.fileExists(atPath: partial.path))
        addTeardownBlock {
            for path in [url, partial] {
                if FileManager.default.fileExists(atPath: path.path) { try FileManager.default.removeItem(at: path) }
            }
        }
        let writer = try NativeHEVCWriter(options: RecordingOptions(windowID: 1, outputURL: url, durationSeconds: 0.1), width: 128, height: 72)
        var buffer: CVPixelBuffer?
        XCTAssertEqual(CVPixelBufferCreate(kCFAllocatorDefault, 128, 72, kCVPixelFormatType_32BGRA,
            [kCVPixelBufferIOSurfacePropertiesKey: [:]] as CFDictionary, &buffer), kCVReturnSuccess)
        let pixels = try XCTUnwrap(buffer)
        CVPixelBufferLockBaseAddress(pixels, [])
        memset(CVPixelBufferGetBaseAddress(pixels), 110, CVPixelBufferGetDataSize(pixels))
        CVPixelBufferUnlockBaseAddress(pixels, [])
        for slot in 0..<6 {
            try writer.append(pixels, slot: slot)
            try await Task.sleep(nanoseconds: 16_666_667)
        }
        let sentinel = Data("created by a different publisher".utf8)
        try sentinel.write(to: url, options: .withoutOverwriting)
        do {
            try await writer.finish()
            XCTFail("Publication overwrote a concurrent destination.")
        } catch let error as RecordingErrorInfo {
            XCTAssertEqual(error.code, "output_exists")
        }
        XCTAssertEqual(try Data(contentsOf: url), sentinel)
        XCTAssertEqual(writer.preservedURL, partial)
        let asset = AVURLAsset(url: partial)
        let tracks = try await asset.loadTracks(withMediaType: .video)
        let track = try XCTUnwrap(tracks.first)
        let formats = try await track.load(.formatDescriptions)
        XCTAssertEqual(CMFormatDescriptionGetMediaSubType(try XCTUnwrap(formats.first)), kCMVideoCodecType_HEVC)
        let reader = try AVAssetReader(asset: asset)
        let output = AVAssetReaderTrackOutput(track: track, outputSettings: [kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA])
        reader.add(output)
        XCTAssertTrue(reader.startReading())
        var count = 0
        while output.copyNextSampleBuffer() != nil { count += 1 }
        XCTAssertEqual(reader.status, .completed)
        XCTAssertEqual(count, 6)
    }
}
