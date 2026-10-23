import AVFoundation
import CoreVideo
import Foundation
import XCTest
@testable import HelengineRecordingApple

/// Verifies the real hardware encoder and compressed MP4 path using only synthetic pixels.
final class NativeHEVCWriterTests: XCTestCase {
    /// Writes and decodes a 60/1 HEVC clip without starting ScreenCaptureKit or obtaining screen permission.
    func testHardwareHEVCProducesSixtyDecodableCFRFrames() async throws {
        let url = URL(fileURLWithPath: "/private/tmp/helengine/macos/recording/encoder-fixture.mp4")
        for path in [url, url.deletingPathExtension().appendingPathExtension("partial.mp4")] {
            if FileManager.default.fileExists(atPath: path.path) { try FileManager.default.removeItem(at: path) }
        }
        let options = RecordingOptions(windowID: 1, outputURL: url, durationSeconds: 1)
        let writer = try NativeHEVCWriter(options: options, width: 128, height: 72)
        XCTAssertTrue(writer.hardwareEncoder)
        XCTAssertFalse(writer.encoderID.isEmpty)
        print("Verified HEVC hardware encoder: \(writer.encoderID)")
        var buffer: CVPixelBuffer?
        XCTAssertEqual(CVPixelBufferCreate(kCFAllocatorDefault, 128, 72, kCVPixelFormatType_32BGRA,
            [kCVPixelBufferIOSurfacePropertiesKey: [:]] as CFDictionary, &buffer), kCVReturnSuccess)
        let pixels = try XCTUnwrap(buffer)
        CVPixelBufferLockBaseAddress(pixels, [])
        memset(CVPixelBufferGetBaseAddress(pixels), 90, CVPixelBufferGetDataSize(pixels))
        CVPixelBufferUnlockBaseAddress(pixels, [])
        for slot in 0..<60 {
            try writer.append(pixels, slot: slot)
            try await Task.sleep(nanoseconds: 16_666_667)
        }
        try await writer.finish()
        XCTAssertEqual(writer.encodedFrames, 60)
        let asset = AVURLAsset(url: url)
        let tracks = try await asset.loadTracks(withMediaType: .video)
        let track = try XCTUnwrap(tracks.first)
        let formats = try await track.load(.formatDescriptions)
        let format = try XCTUnwrap(formats.first)
        XCTAssertEqual(CMFormatDescriptionGetMediaSubType(format), kCMVideoCodecType_HEVC)
        XCTAssertEqual(CMFormatDescriptionGetExtension(format, extensionKey: kCMFormatDescriptionExtension_ColorPrimaries) as? String, kCVImageBufferColorPrimaries_ITU_R_709_2 as String)
        XCTAssertEqual(CMFormatDescriptionGetExtension(format, extensionKey: kCMFormatDescriptionExtension_TransferFunction) as? String, kCVImageBufferTransferFunction_ITU_R_709_2 as String)
        XCTAssertEqual(CMFormatDescriptionGetExtension(format, extensionKey: kCMFormatDescriptionExtension_YCbCrMatrix) as? String, kCVImageBufferYCbCrMatrix_ITU_R_709_2 as String)
        let rate = try await track.load(.nominalFrameRate)
        XCTAssertEqual(rate, 60, accuracy: 0.01)
        let reader = try AVAssetReader(asset: asset)
        let output = AVAssetReaderTrackOutput(track: track, outputSettings: [kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA])
        reader.add(output)
        XCTAssertTrue(reader.startReading())
        var count = 0
        while let sample = output.copyNextSampleBuffer() {
            XCTAssertEqual(CMTimeGetSeconds(CMSampleBufferGetPresentationTimeStamp(sample)), Double(count) / 60, accuracy: 0.000001)
            count += 1
        }
        XCTAssertEqual(reader.status, .completed, reader.error?.localizedDescription ?? "")
        XCTAssertEqual(count, 60)
    }
    /// Rejects a foreign destination before creating any encoder or partial output.
    func testExistingOutputRemainsByteIdentical() throws {
        let url = URL(fileURLWithPath: "/private/tmp/helengine/macos/recording/sentinel.mp4")
        let sentinel = Data("existing foreign video sentinel".utf8)
        try sentinel.write(to: url)
        defer { try? FileManager.default.removeItem(at: url) }
        XCTAssertThrowsError(try NativeHEVCWriter(options: RecordingOptions(windowID: 1, outputURL: url), width: 128, height: 72)) { error in
            XCTAssertEqual((error as? RecordingErrorInfo)?.code, "output_exists")
        }
        XCTAssertEqual(try Data(contentsOf: url), sentinel)
        XCTAssertFalse(FileManager.default.fileExists(atPath: url.deletingPathExtension().appendingPathExtension("partial.mp4").path))
    }

}
