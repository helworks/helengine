import AVFoundation
import CoreVideo
import Darwin
import Foundation
import VideoToolbox

/// Encodes bounded native buffers with hardware HEVC and muxes compressed samples without re-encoding.
/// Callback state is locked; the single lifecycle owner drains all callbacks before asynchronous finalization.
/// Sendable bridges the final SDK completion, whose only access retains this context until the callback returns.
final class NativeHEVCWriter: @unchecked Sendable {
    /// Protects callback state shared with VideoToolbox's encoder threads.
    private let lock = NSLock()
    /// Owns the hardware compression session until its callbacks are drained.
    private var session: VTCompressionSession?
    /// Writes only to the reserved fixed partial path.
    private let assetWriter: AVAssetWriter
    /// Receives compressed samples after their native format description is available.
    private var input: AVAssetWriterInput?
    /// Stores the requested final path, which must not exist.
    private let outputURL: URL
    /// Holds a finalized prefix on failure without overwriting any destination.
    let partialURL: URL
    /// Enforces the fixed source buffer dimensions.
    private let width: Int
    /// Enforces the fixed source buffer dimensions.
    private let height: Int
    /// Bounds native buffers and wakes submissions only after callback completion.
    private let capacity = EncoderCapacity()
    /// Retains the first callback failure so later cleanup cannot mask it.
    private var callbackFailure: RecordingErrorInfo?
    /// Counts samples accepted by the MP4 writer.
    private var sampleCount = 0
    /// Prevents frame submissions after draining starts.
    private var finishing = false
    /// Records whether the hardware-use property was verified.
    private var confirmedHardware = false
    /// Records the encoder ID reported by VideoToolbox.
    private var nativeEncoderID = ""
    /// Records whether finalization produced a playable partial file.
    private var finalized = false
    /// Is true after MP4 finalization timed out while its SDK callback still owns this context.
    private(set) var finalizationPending = false
    /// Records whether exclusive publication succeeded.
    private var published = false

    /// Counts successfully appended encoded samples.
    var encodedFrames: Int { lock.lock(); defer { lock.unlock() }; return sampleCount }
    /// Reports verified hardware use rather than the requested preference.
    var hardwareEncoder: Bool { confirmedHardware }
    /// Exposes the encoder selected by VideoToolbox.
    var encoderID: String { nativeEncoderID }
    /// Names the playable file retained after successful finalization.
    var preservedURL: URL? { finalized && sampleCount > 0 ? (published ? outputURL : partialURL) : nil }

    /// Creates a hardware-required encoder only after protecting both existing output paths.
    init(options: RecordingOptions, width: Int, height: Int) throws {
        try options.validate()
        guard width > 0, height > 0, width % 2 == 0, height % 2 == 0,
              width <= Int(Int32.max), height <= Int(Int32.max) else {
            throw RecordingErrorInfo(code: "unsupported_dimensions", message: "HEVC requires positive even native-pixel dimensions; recording never upscales or pads the source.")
        }
        self.width = width
        self.height = height
        outputURL = options.outputURL
        partialURL = options.outputURL.deletingPathExtension().appendingPathExtension("partial.mp4")
        guard !FileManager.default.fileExists(atPath: outputURL.path),
              !FileManager.default.fileExists(atPath: partialURL.path) else {
            throw RecordingErrorInfo(code: "output_exists", message: "Refusing to overwrite an existing destination or fixed .partial file.")
        }
        let parent = outputURL.deletingLastPathComponent()
        var isDirectory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: parent.path, isDirectory: &isDirectory), isDirectory.boolValue else {
            throw RecordingErrorInfo(code: "output_directory_missing", message: "The output directory must already exist.")
        }
        assetWriter = try AVAssetWriter(outputURL: partialURL, fileType: .mp4)
        assetWriter.movieTimeScale = 60
        let specification = [kVTVideoEncoderSpecification_RequireHardwareAcceleratedVideoEncoder: true] as CFDictionary
        let attributes = [kCVPixelBufferWidthKey: width, kCVPixelBufferHeightKey: height,
                          kCVPixelBufferIOSurfacePropertiesKey: [:]] as CFDictionary
        let status = VTCompressionSessionCreate(allocator: kCFAllocatorDefault, width: Int32(width), height: Int32(height),
            codecType: kCMVideoCodecType_HEVC, encoderSpecification: specification, imageBufferAttributes: attributes,
            compressedDataAllocator: nil, outputCallback: { context, _, status, flags, sample in
                guard let context else { return }
                Unmanaged<NativeHEVCWriter>.fromOpaque(context).takeUnretainedValue().receive(status: status, flags: flags, sample: sample)
            }, refcon: Unmanaged.passUnretained(self).toOpaque(), compressionSessionOut: &session)
        try check(status, operation: "create hardware HEVC encoder")
        guard let session else { throw RecordingErrorInfo(code: "encoder_missing", message: "VideoToolbox returned no compression session.") }
        do {
            try set(kVTCompressionPropertyKey_AverageBitRate, NSNumber(value: options.bitrate))
            try set(kVTCompressionPropertyKey_DataRateLimits, [Double(options.maxBitrate) / 8, 1] as CFArray)
            try set(kVTCompressionPropertyKey_ExpectedFrameRate, NSNumber(value: 60))
            try set(kVTCompressionPropertyKey_MaxKeyFrameInterval, NSNumber(value: 120))
            try set(kVTCompressionPropertyKey_MaxKeyFrameIntervalDuration, NSNumber(value: 2))
            try set(kVTCompressionPropertyKey_ColorPrimaries, kCVImageBufferColorPrimaries_ITU_R_709_2)
            try set(kVTCompressionPropertyKey_TransferFunction, kCVImageBufferTransferFunction_ITU_R_709_2)
            try set(kVTCompressionPropertyKey_YCbCrMatrix, kCVImageBufferYCbCrMatrix_ITU_R_709_2)
            try set(kVTCompressionPropertyKey_RealTime, kCFBooleanTrue)
            try set(kVTCompressionPropertyKey_AllowFrameReordering, kCFBooleanFalse)
            try set(kVTCompressionPropertyKey_ProfileLevel, kVTProfileLevel_HEVC_Main_AutoLevel)
            try check(VTCompressionSessionPrepareToEncodeFrames(session), operation: "prepare HEVC encoder")
            let hardware = try copyProperty(kVTCompressionPropertyKey_UsingHardwareAcceleratedVideoEncoder)
            guard (hardware as? Bool) == true else {
                throw RecordingErrorInfo(code: "hardware_required", message: "VideoToolbox did not confirm hardware HEVC encoding.")
            }
            confirmedHardware = true
            let identity = try copyProperty(kVTCompressionPropertyKey_EncoderID)
            guard let identity = identity as? String, !identity.isEmpty else {
                throw RecordingErrorInfo(code: "encoder_identity_missing", message: "VideoToolbox did not return a nonempty encoder identity.")
            }
            nativeEncoderID = identity
        } catch {
            VTCompressionSessionInvalidate(session)
            self.session = nil
            throw error
        }
    }

    /// Releases the native session after all ordinary finish paths have completed.
    deinit {
        if let session { VTCompressionSessionInvalidate(session) }
    }

    /// Submits one native-sized buffer at its exact 60/1 timestamp with at most four in flight.
    func append(_ pixelBuffer: CVPixelBuffer, slot: Int, deadline: DispatchTime = .now() + .milliseconds(250)) throws {
        guard CVPixelBufferGetWidth(pixelBuffer) == width, CVPixelBufferGetHeight(pixelBuffer) == height else {
            throw RecordingErrorInfo(code: "window_resized", message: "Source dimensions changed during fixed-size recording.")
        }
        try capacity.acquire(deadline: deadline)
        lock.lock()
        if let callbackFailure { lock.unlock(); capacity.release(); throw callbackFailure }
        guard !finishing, let session else { lock.unlock(); capacity.release(); throw RecordingErrorInfo(code: "encoder_closed", message: "Encoding has already ended.") }
        lock.unlock()
        var flags = VTEncodeInfoFlags()
        let status = VTCompressionSessionEncodeFrame(session, imageBuffer: pixelBuffer,
            presentationTimeStamp: CMTime(value: Int64(slot), timescale: 60), duration: CMTime(value: 1, timescale: 60),
            frameProperties: nil, sourceFrameRefcon: nil, infoFlagsOut: &flags)
        if status != noErr {
            capacity.release()
            try check(status, operation: "encode HEVC frame")
        }
    }

    /// Drains compression, finalizes a valid prefix, and publishes exclusively only on complete success.
    func finish(publish: Bool = true) async throws {
        try drain()
        let failure = callbackFailure
        let frameCount = sampleCount
        let currentInput = input
        guard frameCount > 0, let currentInput else {
            assetWriter.cancelWriting()
            throw failure ?? RecordingErrorInfo(code: "no_encoded_frames", message: "No compressed samples were available to finalize.")
        }
        currentInput.markAsFinished()
        assetWriter.endSession(atSourceTime: CMTime(value: Int64(frameCount), timescale: 60))
        do {
            let _: Void = try await AsyncDeadline.run(seconds: 5, timeoutCode: "mux_finalize_timeout") { [self] complete in
                assetWriter.finishWriting { [self] in
                    withExtendedLifetime(self) { complete(.success(())) }
                }
            }
        } catch {
            finalizationPending = true
            throw error
        }
        guard assetWriter.status == .completed else {
            throw failure ?? RecordingErrorInfo(code: "mux_finalize_failed", message: assetWriter.error?.localizedDescription ?? "MP4 finalization did not complete.")
        }
        finalized = true
        if let failure { throw failure }
        if publish {
            let result = partialURL.path.withCString { source in outputURL.path.withCString { destination in Darwin.link(source, destination) } }
            guard result == 0 else {
                throw RecordingErrorInfo(code: errno == EEXIST ? "output_exists" : "output_publish_failed", message: "Exclusive MP4 publication failed with errno \(errno); finalized prefix remains at \(partialURL.path).")
            }
            published = true
            guard Darwin.unlink(partialURL.path) == 0 else {
                throw RecordingErrorInfo(code: "partial_cleanup_failed", message: "MP4 published but partial-link cleanup failed with errno \(errno).")
            }
        }
    }

    /// Stops submissions and waits for every native callback before asynchronous mux finalization.
    private func drain() throws {
        lock.lock()
        if finishing { lock.unlock(); throw RecordingErrorInfo(code: "encoder_closed", message: "The encoder was already finalized.") }
        finishing = true
        let currentSession = session
        lock.unlock()
        if let currentSession {
            let status = VTCompressionSessionCompleteFrames(currentSession, untilPresentationTimeStamp: .invalid)
            VTCompressionSessionInvalidate(currentSession)
            session = nil
            if status != noErr && callbackFailure == nil {
                callbackFailure = RecordingErrorInfo(code: "encoder_drain_failed", message: "VideoToolbox drain returned OSStatus \(status).")
            }
        }
    }

    /// Transfers a retained CF property through a typed unmanaged pointer matching the C ownership contract.
    private func copyProperty(_ key: CFString) throws -> CFTypeRef {
        guard let session else { throw RecordingErrorInfo(code: "encoder_missing", message: "The compression session is unavailable.") }
        let storage = UnsafeMutablePointer<Unmanaged<CFTypeRef>?>.allocate(capacity: 1)
        storage.initialize(to: nil)
        defer {
            storage.pointee?.release()
            storage.deinitialize(count: 1)
            storage.deallocate()
        }
        try check(VTSessionCopyProperty(session, key: key, allocator: kCFAllocatorDefault, valueOut: storage), operation: "read \(key)")
        guard let value = storage.pointee else {
            throw RecordingErrorInfo(code: "encoder_property_missing", message: "VideoToolbox returned no value for \(key).")
        }
        let result = value.takeRetainedValue()
        storage.pointee = nil
        return result
    }

    /// Applies one required encoder property and rejects unsupported controls.
    private func set(_ key: CFString, _ value: CFTypeRef) throws {
        guard let session else { throw RecordingErrorInfo(code: "encoder_missing", message: "The compression session is unavailable.") }
        try check(VTSessionSetProperty(session, key: key, value: value), operation: "set \(key)")
    }

    /// Converts native status codes without hiding their values.
    private func check(_ status: OSStatus, operation: String) throws {
        guard status == noErr else { throw RecordingErrorInfo(code: "encoder_failed", message: "Failed to \(operation): OSStatus \(status).") }
    }

    /// Appends compressed callback samples directly to MP4 while holding a bounded callback state.
    private func receive(status: OSStatus, flags: VTEncodeInfoFlags, sample: CMSampleBuffer?) {
        lock.lock()
        defer {
            capacity.release(failure: callbackFailure)
            lock.unlock()
        }
        guard callbackFailure == nil else { return }
        guard status == noErr, !flags.contains(.frameDropped), let sample, CMSampleBufferDataIsReady(sample) else {
            callbackFailure = RecordingErrorInfo(code: "encoder_callback_failed", message: "HEVC callback returned status \(status), dropped=\(flags.contains(.frameDropped)).")
            return
        }
        if input == nil {
            guard let format = CMSampleBufferGetFormatDescription(sample),
                  CMFormatDescriptionGetMediaSubType(format) == kCMVideoCodecType_HEVC else {
                callbackFailure = RecordingErrorInfo(code: "invalid_encoded_format", message: "Encoder callback did not provide HEVC format metadata.")
                return
            }
            let newInput = AVAssetWriterInput(mediaType: .video, outputSettings: nil, sourceFormatHint: format)
            newInput.expectsMediaDataInRealTime = true
            newInput.mediaTimeScale = 60
            guard assetWriter.canAdd(newInput) else {
                callbackFailure = RecordingErrorInfo(code: "mux_input_failed", message: "MP4 writer rejected compressed HEVC samples.")
                return
            }
            assetWriter.add(newInput)
            guard assetWriter.startWriting() else {
                callbackFailure = RecordingErrorInfo(code: "mux_start_failed", message: assetWriter.error?.localizedDescription ?? "MP4 writer did not start.")
                return
            }
            assetWriter.startSession(atSourceTime: .zero)
            input = newInput
        }
        guard let input, input.isReadyForMoreMediaData else {
            callbackFailure = RecordingErrorInfo(code: "writer_backpressure", message: "MP4 writer was not ready; no unbounded sample queue was created.")
            return
        }
        guard input.append(sample) else {
            callbackFailure = RecordingErrorInfo(code: "mux_append_failed", message: assetWriter.error?.localizedDescription ?? "Compressed HEVC sample append failed.")
            return
        }
        sampleCount += 1
    }
}
