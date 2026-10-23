import CoreGraphics
import Foundation
import ScreenCaptureKit

/// Decodes the documented CoreGraphics dictionary representation carried by SCK attachments.
enum FrameMetadata {
    /// Extracts original window bounds without substituting screen or content bounds.
    static func windowBounds(_ info: [SCStreamFrameInfo: Any]) throws -> CGRect {
        let bounds: CGRect?
        if let rectangle = info[.screenRect] as? CGRect { bounds = rectangle }
        else if let dictionary = info[.screenRect] as? [String: Any] {
            bounds = CGRect(dictionaryRepresentation: dictionary as CFDictionary)
        } else { bounds = nil }
        guard let bounds, bounds.width.isFinite, bounds.height.isFinite, bounds.width > 0, bounds.height > 0 else {
            let keys = info.keys.map { $0.rawValue }.sorted().joined(separator: ",")
            let observedType = info[.screenRect].map { String(reflecting: type(of: $0)) } ?? "missing"
            throw RecordingErrorInfo(code: "frame_metadata_missing", message: "Invalid original screenRect metadata (type=\(observedType), keys=\(keys)); expected a CoreGraphics rectangle dictionary.")
        }
        return bounds
    }
}
