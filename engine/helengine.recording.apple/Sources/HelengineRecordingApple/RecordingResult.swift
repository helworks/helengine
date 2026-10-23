import Foundation

/// Carries success or an explicit failure together with the preserved recording prefix.
public struct RecordingResult: Codable {
    /// Is true only when all requested slots were finalized successfully.
    public let success: Bool
    /// Names the exact requested MP4 output.
    public let outputPath: String
    /// States whether a playable finalized prefix was retained after failure.
    public let prefixPreserved: Bool
    /// Names the finalized file actually preserved after a failure.
    public let prefixPath: String?
    /// Is true when native cleanup has not quiesced; no playable-prefix claim is made.
    public let cleanupPending: Bool
    /// Describes the original failure without replacing it with cleanup status.
    public let error: RecordingErrorInfo?
    /// Describes actual capture, duplication, encoder, and output counts.
    public let stats: RecordingStatistics

    /// Creates the JSON receipt consumed by CLI clients.
    public init(success: Bool, outputPath: String, prefixPreserved: Bool, error: RecordingErrorInfo?, stats: RecordingStatistics, prefixPath: String? = nil, cleanupPending: Bool = false) {
        self.cleanupPending = cleanupPending
        self.prefixPath = prefixPath
        self.success = success
        self.outputPath = outputPath
        self.prefixPreserved = prefixPreserved
        self.error = error
        self.stats = stats
    }
}
