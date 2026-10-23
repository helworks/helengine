import Foundation

/// Publishes one UTF-8 result and optionally an atomic file for LaunchServices callers.
enum RecorderJSON {
    /// Writes an intermediate ownership receipt only to the automation file, keeping stdout for the final result.
    static func writeStarted(_ value: RecorderStartReport, path: String?) throws {
        guard let path = path else { return }
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        try encoder.encode(value).write(to: URL(fileURLWithPath: path), options: .atomic)
    }
    /// Emits Codable metadata without serializing captured pixels.
    static func write<Value: Encodable>(_ value: Value, path: String?) throws {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        let data = try encoder.encode(value)
        if let path = path { try data.write(to: URL(fileURLWithPath: path), options: .atomic) }
        FileHandle.standardOutput.write(data)
        FileHandle.standardOutput.write(Data([10]))
    }
}
