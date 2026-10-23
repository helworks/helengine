import Foundation

/// Reports encoded CFR slots separately from the source window's actual updates.
public struct RecordingStatistics: Codable {
    /// Records the fixed native-pixel output width.
    public internal(set) var width = 0
    /// Records the fixed native-pixel output height.
    public internal(set) var height = 0
    /// Specifies the CFR numerator, independent of source update frequency.
    public let frameRateNumerator: Int
    /// Specifies the CFR denominator.
    public let frameRateDenominator: Int
    /// Counts the planned timeline slots.
    public let targetFrames: Int
    /// Counts compressed samples successfully appended to the MP4.
    public internal(set) var encodedFrames = 0
    /// Counts slots that reused the most recent source frame.
    public internal(set) var duplicateFrames = 0
    /// Counts complete source-window frames delivered by ScreenCaptureKit.
    public internal(set) var sourceFrames = 0
    /// Counts source frames replaced before any output slot consumed them.
    public internal(set) var droppedSourceFrames = 0
    /// Measures finalized output duration from its actual encoded frame count.
    public internal(set) var durationSeconds: Double = 0
    /// Records the requested average bitrate in bits per second.
    public let bitrate: Int64
    /// Records the requested one-second bitrate cap.
    public let maxBitrate: Int64
    /// Is true only after VideoToolbox confirms hardware encoding is in use.
    public internal(set) var hardwareEncoder = false
    /// Identifies the encoder reported by VideoToolbox when available.
    public internal(set) var encoderID = ""

    /// Starts an empty receipt before capture or encoder setup.
    public init(options: RecordingOptions) {
        targetFrames = options.durationSeconds.isFinite && options.durationSeconds > 0 && options.durationSeconds < Double(Int.max / 60) ? Int(ceil(options.durationSeconds * 60)) : 0
        frameRateNumerator = 60
        frameRateDenominator = 1
        bitrate = options.bitrate
        maxBitrate = options.maxBitrate
    }
}
