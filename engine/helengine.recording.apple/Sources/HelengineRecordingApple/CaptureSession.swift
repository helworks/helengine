import CoreGraphics
import CoreMedia
import CoreVideo
import Foundation
import ScreenCaptureKit

/// Serializes mutable capture state on its private queue; only the separately locked writer crosses native callback threads.
/// The unchecked Sendable conformance bridges SDK delegates while queue ownership protects every mutable capture field.
final class CaptureSession: NSObject, SCStreamOutput, SCStreamDelegate, @unchecked Sendable {
    /// Keeps capture callbacks and scheduling decisions in a single ordered domain.
    private let queue = DispatchQueue(label: "com.helworks.recording.apple.capture")
    /// Stores the explicit operation contract.
    private let options: RecordingOptions
    /// Encodes compressed samples with bounded native backpressure.
    private let writer: NativeHEVCWriter
    /// Validates original pixel and point dimensions.
    private let geometry: CaptureGeometry
    /// Defines all output timestamps without accumulating fractional drift.
    private var timeline: FrameTimeline
    /// Distinguishes source updates from repeats and overwritten updates.
    private var accounting = FrameAccounting()
    /// Owns only the selected desktop-independent window stream.
    private var stream: SCStream!
    /// Retains the newest delivered source buffer, including a possibly future observation.
    private var latestBuffer: CVPixelBuffer?
    /// Retains the previously selected buffer for earlier slots that cannot consume the newest observation.
    private var usedBuffer: CVPixelBuffer?
    /// Defines timeline zero from the first complete source observation.
    private var startHostTime: UInt64?
    /// Schedules the output slots on the same queue as source delivery.
    private var timer: DispatchSourceTimer?
    /// Completes the single public async operation after native resources are stopped.
    private var continuation: CheckedContinuation<RecordingResult, Error>?
    /// Prevents duplicate completion from timeout, cancellation, and delegate failures.
    private var stopping = false
    /// Indicates that startCapture has completed, including a failed start.
    private var startCompleted = false
    /// Tracks a native start or teardown operation that has not quiesced.
    private var cleanupPending = false
    /// Records a requested stop that arrived while capture was starting.
    private var pendingStop: RecordingErrorInfo?
    /// Stores capture counters independent of asynchronous encoder callbacks.
    private var stats: RecordingStatistics

    /// Creates the fixed-size stream and native encoder without starting capture.
    init(options: RecordingOptions, window: SCWindow) throws {
        self.options = options
        timeline = FrameTimeline(durationSeconds: options.durationSeconds)
        let filter = SCContentFilter(desktopIndependentWindow: window)
        let width = Int(ceil(filter.contentRect.width * Double(filter.pointPixelScale)))
        let height = Int(ceil(filter.contentRect.height * Double(filter.pointPixelScale)))
        geometry = CaptureGeometry(width: width, height: height, windowWidth: window.frame.width, windowHeight: window.frame.height)
        writer = try NativeHEVCWriter(options: options, width: width, height: height)
        stats = RecordingStatistics(options: options)
        stats.width = width
        stats.height = height
        stats.hardwareEncoder = writer.hardwareEncoder
        stats.encoderID = writer.encoderID
        super.init()
        let config = SCStreamConfiguration()
        config.width = width
        config.height = height
        config.minimumFrameInterval = CMTime(value: 1, timescale: 60)
        config.pixelFormat = kCVPixelFormatType_32BGRA
        config.colorSpaceName = CGColorSpace.sRGB
        config.scalesToFit = false
        config.preservesAspectRatio = true
        config.showsCursor = false
        config.capturesAudio = false
        config.queueDepth = 3
        config.ignoreShadowsSingleWindow = true
        if #available(macOS 14.2, *) { config.includeChildWindows = false }
        if #available(macOS 15.0, *) { config.captureDynamicRange = .SDR }
        stream = SCStream(filter: filter, configuration: config, delegate: self)
    }

    /// Runs until the exact slot count completes or one explicit failure stops recording.
    func run() async throws -> RecordingResult {
        try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { continuation in
                queue.async {
                    self.continuation = continuation
                    if self.stopping { self.finishStop(); return }
                    self.begin()
                }
            }
        } onCancel: {
            self.queue.async { self.requestStop(RecordingErrorInfo(code: "cancelled", message: "Recording was cancelled.")) }
        }
    }

    /// Adds the single stream output and starts the first-frame deadline.
    private func begin() {
        do { try stream.addStreamOutput(self, type: .screen, sampleHandlerQueue: queue) }
        catch {
            startCompleted = true
            requestStop(issue(error, code: "capture_output_failed"))
            return
        }
        queue.asyncAfter(deadline: .now() + 10) { [weak self] in
            guard let self else { return }
            if self.startHostTime == nil && !self.stopping {
                self.requestStop(RecordingErrorInfo(code: "first_frame_timeout", message: "No complete window frame arrived within 10 seconds."))
            }
        }
        let currentStream = stream!
        Task {
            var startError: RecordingErrorInfo?
            do {
                let _: Void = try await AsyncDeadline.run(seconds: 10, timeoutCode: "capture_start_timeout",
                    onLateCompletion: { [self] result in cleanupLateStart(currentStream, result: result) }) { [self] complete in
                    currentStream.startCapture { [self] error in
                        queue.async { self.startCompleted = true }
                        if let error { complete(.failure(error)) }
                        else { complete(.success(())) }
                    }
                }
            } catch { startError = issue(error, code: "capture_start_failed") }
            let observedError = startError
            queue.async {
                if observedError?.code == "capture_start_timeout" { self.cleanupPending = true }
                if self.stopping {
                    if let observedError { self.pendingStop = self.combine(self.pendingStop, observedError) }
                    self.finishStop()
                } else if let observedError { self.requestStop(observedError) }
            }
        }
    }

    /// Receives only the chosen window and validates metadata before retaining its latest buffer.
    func stream(_ stream: SCStream, didOutputSampleBuffer sampleBuffer: CMSampleBuffer, of type: SCStreamOutputType) {
        guard !stopping, type == .screen else { return }
        guard sampleBuffer.isValid,
              let attachments = CMSampleBufferGetSampleAttachmentsArray(sampleBuffer, createIfNecessary: false) as? [[SCStreamFrameInfo: Any]],
              let info = attachments.first,
              let rawStatus = info[.status] as? Int,
              let status = SCFrameStatus(rawValue: rawStatus) else {
            requestStop(RecordingErrorInfo(code: "invalid_frame", message: "ScreenCaptureKit returned incomplete frame metadata."))
            return
        }
        if status == .idle || status == .started { return }
        guard status == .complete else {
            requestStop(RecordingErrorInfo(code: "window_unavailable", message: "Window capture became blank, suspended, or stopped (status \(rawStatus))."))
            return
        }
        guard let pixels = CMSampleBufferGetImageBuffer(sampleBuffer) else {
            requestStop(RecordingErrorInfo(code: "frame_pixels_missing", message: "The complete window frame lacks its native pixel buffer."))
            return
        }
        do {
            let screenRect = try FrameMetadata.windowBounds(info)
            try geometry.validate(pixelWidth: CVPixelBufferGetWidth(pixels), pixelHeight: CVPixelBufferGetHeight(pixels),
                observedWindowWidth: screenRect.width, observedWindowHeight: screenRect.height)
        } catch { requestStop(issue(error, code: "window_resized")); return }
        let observedTime = DispatchTime.now().uptimeNanoseconds
        if startHostTime == nil { startHostTime = observedTime }
        guard let startHostTime else { return }
        accounting.receiveSourceFrame(observedSeconds: Double(observedTime - startHostTime) / 1_000_000_000)
        latestBuffer = pixels
        if usedBuffer == nil { usedBuffer = pixels }
        if timer == nil {
            let timer = DispatchSource.makeTimerSource(queue: queue)
            timer.schedule(deadline: .now(), repeating: .nanoseconds(16_666_667), leeway: .nanoseconds(0))
            timer.setEventHandler { [weak self] in self?.tick() }
            self.timer = timer
            timer.resume()
        }
    }

    /// Converts native capture failure into one terminal receipt.
    func stream(_ stream: SCStream, didStopWithError error: Error) {
        queue.async { self.requestStop(self.issue(error, code: "capture_stopped")) }
    }

    /// Encodes eligible source observations at exact CFR timestamps without interpolation.
    private func tick() {
        guard !stopping, let startHostTime else { return }
        do {
            let elapsed = Double(DispatchTime.now().uptimeNanoseconds - startHostTime) / 1_000_000_000
            for slot in try timeline.takeDueSlots(elapsedSeconds: elapsed) {
                let selectedSequence = try accounting.consumeSlot(slotSeconds: Double(slot) / 60)
                if selectedSequence == accounting.sourceFrames { usedBuffer = latestBuffer }
                guard let usedBuffer else {
                    throw RecordingErrorInfo(code: "eligible_frame_missing", message: "No observed source frame is eligible for this CFR slot.")
                }
                try writer.append(usedBuffer, slot: slot)
            }
            if timeline.nextSlot == timeline.targetFrames { requestStop(nil) }
        } catch { requestStop(issue(error, code: "recording_failed")) }
    }

    /// Marks a terminal outcome once and stops scheduling immediately.
    private func requestStop(_ failure: RecordingErrorInfo?) {
        guard !stopping else { return }
        stopping = true
        pendingStop = failure
        timer?.cancel()
        timer = nil
        if !startCompleted { cleanupPending = true }
        finishStop()
    }

    /// Stops native capture, drains the encoder, and retains a finalized prefix on failure.
    private func finishStop() {
        guard continuation != nil else { return }
        let continuation = self.continuation
        self.continuation = nil
        let requestedFailure = pendingStop
        let currentStream = stream
        Task {
            var failure = requestedFailure
            var stopPending = false
            if let currentStream {
                do {
                    let _: Void = try await AsyncDeadline.run(seconds: 5, timeoutCode: "capture_stop_timeout",
                        onLateCompletion: { [self] result in cleanupLateStop(currentStream, result: result) }) { [self] complete in
                        currentStream.stopCapture { [self] error in
                            withExtendedLifetime(self) {
                                if let error { complete(.failure(error)) }
                                else { complete(.success(())) }
                            }
                        }
                    }
                } catch {
                    stopPending = true
                    failure = combine(failure, issue(error, code: "capture_stop_failed"))
                }
                if !stopPending {
                    do { try currentStream.removeStreamOutput(self, type: .screen) }
                    catch { failure = combine(failure, issue(error, code: "capture_output_remove_failed")) }
                }
            }
            let startFinished: Bool = await withCheckedContinuation { completion in
                queue.async { completion.resume(returning: self.startCompleted) }
            }
            let nativePending = NativeCleanupStatus(startCompleted: startFinished, stopPending: stopPending, writerPending: false).isPending
            if nativePending && failure == nil {
                failure = RecordingErrorInfo(code: "teardown_pending", message: "A native start or stop operation has not quiesced; recording cannot be published as successful.")
            }
            do { try await writer.finish(publish: failure == nil && !nativePending) }
            catch { failure = combine(failure, issue(error, code: "writer_finalize_failed")) }
            let finishedFailure = failure
            let pendingStopOperation = stopPending
            let pendingWriterOperation = writer.finalizationPending
            queue.async {
                self.cleanupPending = NativeCleanupStatus(startCompleted: self.startCompleted, stopPending: pendingStopOperation, writerPending: pendingWriterOperation).isPending
                self.stream = nil
                self.latestBuffer = nil
                self.usedBuffer = nil
                self.stats.sourceFrames = self.accounting.sourceFrames
                self.stats.droppedSourceFrames = self.accounting.droppedSourceFrames
                self.stats.duplicateFrames = self.accounting.duplicateFrames
                self.stats.encodedFrames = self.writer.encodedFrames
                self.stats.durationSeconds = Double(self.stats.encodedFrames) / 60
                let prefix = self.cleanupPending ? nil : self.writer.preservedURL
                let effectiveFailure = finishedFailure ?? (self.cleanupPending ? RecordingErrorInfo(code: "teardown_pending", message: "Native cleanup is pending; no successful recording or playable prefix is claimed.") : nil)
                let result = RecordingResult(success: effectiveFailure == nil, outputPath: self.options.outputURL.path,
                    prefixPreserved: effectiveFailure != nil && prefix != nil, error: effectiveFailure, stats: self.stats,
                    prefixPath: effectiveFailure != nil ? prefix?.path : nil, cleanupPending: self.cleanupPending)
                if effectiveFailure != nil { continuation?.resume(throwing: RecordingFailure(result: result)) }
                else { continuation?.resume(returning: result) }
            }
        }
    }

    /// Stops a start operation that completed only after its deadline, retaining callback context until stop returns.
    private func cleanupLateStart(_ stream: SCStream, result: Result<Void, Error>) {
        switch result {
        case .success:
            stream.stopCapture { [self] error in
                if let error { reportLateCleanup(error) }
                do { try stream.removeStreamOutput(self, type: .screen) }
                catch { reportLateCleanup(error) }
            }
        case .failure(let error):
            reportLateCleanup(error)
        }
    }

    /// Releases stream output only when a late successful stop confirms capture has quiesced.
    private func cleanupLateStop(_ stream: SCStream, result: Result<Void, Error>) {
        switch result {
        case .success:
            do { try stream.removeStreamOutput(self, type: .screen) }
            catch { reportLateCleanup(error) }
        case .failure(let error):
            reportLateCleanup(error)
        }
    }

    /// Makes late SDK failures observable without corrupting the JSON response on stdout.
    private func reportLateCleanup(_ error: Error) {
        FileHandle.standardError.write(Data(("Late recorder cleanup: " + String(describing: error) + "\n").utf8))
    }

    /// Preserves stable library failure codes and includes native error descriptions otherwise.
    private func issue(_ error: Error, code: String) -> RecordingErrorInfo {
        (error as? RecordingErrorInfo) ?? RecordingErrorInfo(code: code, message: String(describing: error))
    }

    /// Keeps the primary failure while reporting any secondary teardown error explicitly.
    private func combine(_ primary: RecordingErrorInfo?, _ secondary: RecordingErrorInfo) -> RecordingErrorInfo {
        guard let primary else { return secondary }
        return RecordingErrorInfo(code: primary.code, message: primary.message + " Teardown [" + secondary.code + "]: " + secondary.message)
    }
}
