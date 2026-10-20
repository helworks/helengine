namespace helengine.capture.windows.Interop;
/// <summary>Validates explicit CPU buffer allocation without overflowing pixel arithmetic.</summary>
static class CapturePixelBufferUtils {
    /// <summary>Returns the exact packed BGRA byte count, rejecting invalid dimensions or array overflow.</summary>
    public static int GetByteCount(int width, int height) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width); ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        return checked(width * height * 4);
    }
}
