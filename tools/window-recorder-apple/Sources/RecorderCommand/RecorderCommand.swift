import Foundation

/// Validated CLI input independent of capture, app activation and encoding.
public struct RecorderCommand {
    /// Operation explicitly chosen by the caller.
    public let action: RecorderAction
    /// Exact window identifier, used only by record.
    public let windowID: UInt32
    /// Absolute MP4 destination, used only by record.
    public let outputPath: String?
    /// Optional absolute JSON automation receipt.
    public let resultPath: String?
    /// Whether the permission command may request OS permission.
    public let requestPermission: Bool
    /// Requested duration, excluding codec preparation.
    public let durationSeconds: Double
    /// Encoder target bitrate in bits per second.
    public let bitrate: Int64
    /// Encoder peak rate in bits per second.
    public let maxBitrate: Int64

    /// Parses before any capture or encoder resource is created.
    public static func parse(_ arguments: [String]) throws -> RecorderCommand {
        guard let first = arguments.first, let action = RecorderAction(rawValue: first) else {
            throw RecorderCommandError.invalid("Choose windows, permission, record, or fixture.")
        }
        var values: [String: String] = [:]
        var request = false
        var index = 1
        while index < arguments.count {
            let key = arguments[index]
            if key == "--request" {
                guard action == .permission, !request else { throw RecorderCommandError.invalid("--request is valid once on permission.") }
                request = true
                index += 1
                continue
            }
            let allowed = key == "--result" || (action == .record && ["--window-id", "--output", "--duration", "--fps", "--bitrate", "--max-bitrate"].contains(key))
            guard allowed, values[key] == nil, index + 1 < arguments.count else {
                throw RecorderCommandError.invalid("Unknown, duplicate, or incomplete option: \(key)")
            }
            values[key] = arguments[index + 1]
            index += 2
        }
        if let result = values["--result"] { try validateAbsolutePath(result, extension: "json") }
        var windowID: UInt32 = 0
        var duration = 30.0
        var bitrate: Int64 = 40_000_000
        var maxBitrate: Int64 = 60_000_000
        if action == .record {
            guard let window = values["--window-id"], let identifier = UInt32(window), identifier > 0,
                  let output = values["--output"] else {
                throw RecorderCommandError.invalid("record requires an explicit positive --window-id and --output.")
            }
            windowID = identifier
            try validateAbsolutePath(output, extension: "mp4")
            if let fps = values["--fps"], fps != "60" { throw RecorderCommandError.invalid("Only 60 FPS is supported.") }
            if let raw = values["--duration"] {
                guard let parsed = Double(raw), parsed.isFinite, parsed > 0, parsed <= Double(Int.max / 60) else {
                    throw RecorderCommandError.invalid("Duration must be finite, positive, and representable.")
                }
                duration = parsed
            }
            if let raw = values["--bitrate"] { bitrate = try parseBitrate(raw) }
            if let raw = values["--max-bitrate"] { maxBitrate = try parseBitrate(raw) }
            guard maxBitrate >= bitrate else { throw RecorderCommandError.invalid("Maximum bitrate must be at least the target bitrate.") }
        }
        return RecorderCommand(action: action, windowID: windowID, outputPath: values["--output"], resultPath: values["--result"], requestPermission: request, durationSeconds: duration, bitrate: bitrate, maxBitrate: maxBitrate)
    }
    /// Rejects relative destinations before Foundation could silently resolve them.
    static func validateAbsolutePath(_ path: String, extension suffix: String) throws {
        guard path.hasPrefix("/"), !path.contains("\u{0000}"), URL(fileURLWithPath: path).pathExtension.lowercased() == suffix else {
            throw RecorderCommandError.invalid("An absolute .\(suffix) path is required: \(path)")
        }
    }
    /// Parses integer bit rates with optional decimal SI multipliers and checked arithmetic.
    static func parseBitrate(_ raw: String) throws -> Int64 {
        let suffix = raw.last?.uppercased()
        let multiplier: Int64 = suffix == "M" ? 1_000_000 : (suffix == "K" ? 1_000 : 1)
        let digits = multiplier == 1 ? raw : String(raw.dropLast())
        guard let number = Int64(digits), number > 0 else { throw RecorderCommandError.invalid("Invalid bitrate: \(raw)") }
        let product = number.multipliedReportingOverflow(by: multiplier)
        guard !product.overflow else { throw RecorderCommandError.invalid("Bitrate overflow: \(raw)") }
        return product.partialValue
    }
}
