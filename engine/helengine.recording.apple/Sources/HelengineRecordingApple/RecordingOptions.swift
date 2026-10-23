import Foundation

/// Configures one exact-window, silent, hardware-HEVC recording.
public struct RecordingOptions {
    /// Identifies the sole window allowed in the stream.
    public let windowID: UInt32
    /// Names the MP4 created exclusively by this operation.
    public let outputURL: URL
    /// Requests this many seconds on the 60/1 output timeline.
    public let durationSeconds: Double
    /// Requests the average video bitrate in bits per second.
    public let bitrate: Int64
    /// Limits encoded bytes over a one-second window.
    public let maxBitrate: Int64

    /// Creates options without starting capture or checking permission.
    public init(windowID: UInt32, outputURL: URL, durationSeconds: Double = 30, bitrate: Int64 = 40_000_000, maxBitrate: Int64 = 60_000_000) {
        self.windowID = windowID
        self.outputURL = outputURL
        self.durationSeconds = durationSeconds
        self.bitrate = bitrate
        self.maxBitrate = maxBitrate
    }

    /// Rejects invalid options before any capture or output operation.
    public func validate() throws {
        guard windowID > 0, outputURL.isFileURL, outputURL.path.hasPrefix("/"),
              outputURL.pathExtension.lowercased() == "mp4",
              durationSeconds.isFinite, durationSeconds > 0,
              durationSeconds < Double(Int.max / 60),
              bitrate > 0, maxBitrate >= bitrate, maxBitrate <= Int64(Int32.max) else {
            throw RecordingErrorInfo(code: "invalid_options", message: "Require a positive exact window ID, absolute MP4 file URL, finite positive duration, and 0 < bitrate <= maxBitrate <= Int32.max.")
        }
    }
}
