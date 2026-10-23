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
    /// Reproduces the portrait simulator's native dimensions using the unchanged four-frame submission cap.
    func testPortraitHardwareEncoderBoundsFourPendingFrames() async throws {
        try await verifyBoundedHardwareEncoding(width: 912, height: 1944, name: "portrait-delay-fixture")
    }

    /// Reproduces the initial Retina PS3 window without capturing a window or reducing its dimensions.
    func testRetinaHardwareEncoderBoundsFourPendingFrames() async throws {
        try await verifyBoundedHardwareEncoding(width: 3420, height: 2146, name: "retina-delay-fixture")
    }

    /// Models due CFR slots submitted together after a bounded scheduler delay, without enlarging the queue.
    func testPortraitHardwareEncoderBoundsCatchUpBurst() async throws {
        try await verifyBoundedHardwareEncoding(width: 912, height: 1944, name: "portrait-delay-fixture", paceNanoseconds: 0, frameCount: 8)
    }

    /// Exercises the same catch-up submission pattern at the original Retina game-window dimensions.
    func testRetinaHardwareEncoderBoundsCatchUpBurst() async throws {
        try await verifyBoundedHardwareEncoding(width: 3420, height: 2146, name: "retina-delay-fixture", paceNanoseconds: 0, frameCount: 8)
    }

    /// Encodes and decodes synthetic native pixels at 60/1 with the actual hardware encoder and bounded submission queue.
    private func verifyBoundedHardwareEncoding(width: Int, height: Int, name: String, paceNanoseconds: UInt64 = 16_666_667, frameCount: Int = 60) async throws {
        let url = URL(fileURLWithPath: "/private/tmp/helengine/macos/recording/\(name).mp4")
        let partial = url.deletingPathExtension().appendingPathExtension("partial.mp4")
        for path in [url, partial] {
            if FileManager.default.fileExists(atPath: path.path) { try FileManager.default.removeItem(at: path) }
        }
        let writer = try NativeHEVCWriter(options: RecordingOptions(windowID: 1, outputURL: url, durationSeconds: 1), width: width, height: height)
        print("Hardware delay fixture \(width)x\(height): encoder=\(writer.encoderID), hardware=\(writer.hardwareEncoder)")
        XCTAssertTrue(writer.hardwareEncoder)
        XCTAssertFalse(writer.encoderID.isEmpty)
        var buffer: CVPixelBuffer?
        XCTAssertEqual(CVPixelBufferCreate(kCFAllocatorDefault, width, height, kCVPixelFormatType_32BGRA,
            [kCVPixelBufferIOSurfacePropertiesKey: [:]] as CFDictionary, &buffer), kCVReturnSuccess)
        let pixels = try XCTUnwrap(buffer)
        CVPixelBufferLockBaseAddress(pixels, [])
        memset(CVPixelBufferGetBaseAddress(pixels), 90, CVPixelBufferGetDataSize(pixels))
        CVPixelBufferUnlockBaseAddress(pixels, [])
        var appendMilliseconds: [Double] = []
        let batchDeadline = DispatchTime.now() + .milliseconds(250)
        do {
            for slot in 0..<frameCount {
                let start = DispatchTime.now().uptimeNanoseconds
                let deadline = paceNanoseconds == 0 ? batchDeadline : DispatchTime.now() + .milliseconds(250)
                try writer.append(pixels, slot: slot, deadline: deadline)
                appendMilliseconds.append(Double(DispatchTime.now().uptimeNanoseconds - start) / 1_000_000)
                if paceNanoseconds > 0 { try await Task.sleep(nanoseconds: paceNanoseconds) }
            }
            try await writer.finish()
        } catch {
            print("Hardware delay fixture failure \(width)x\(height): accepted=\(appendMilliseconds.count), appendMilliseconds=\(appendMilliseconds), error=\(error)")
            // Drain all callbacks before allowing the session owner to leave scope after a failed submission.
            try? await writer.finish(publish: false)
            throw error
        }
        XCTAssertEqual(writer.encodedFrames, frameCount)
        let asset = AVURLAsset(url: url)
        let tracks = try await asset.loadTracks(withMediaType: .video)
        let track = try XCTUnwrap(tracks.first)
        let formats = try await track.load(.formatDescriptions)
        let format = try XCTUnwrap(formats.first)
        XCTAssertEqual(CMFormatDescriptionGetMediaSubType(format), kCMVideoCodecType_HEVC)
        let dimensions = CMVideoFormatDescriptionGetDimensions(format)
        XCTAssertEqual(dimensions.width, Int32(width))
        XCTAssertEqual(dimensions.height, Int32(height))
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
        XCTAssertEqual(count, frameCount)
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
