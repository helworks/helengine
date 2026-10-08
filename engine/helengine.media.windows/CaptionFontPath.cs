namespace helengine.media.windows;

/// <summary>
/// Resolves the font file a caption style names against the owned assets root, rejecting rooted paths, drive
/// specifiers, escapes out of the root and reparse points, so text rendering and text measurement load the same file.
/// </summary>
public static class CaptionFontPath {
    /// <summary>
    /// Resolves one style font file.
    /// </summary>
    /// <param name="assetsRoot">Owned assets root; required because fonts are never loaded from arbitrary paths.</param>
    /// <param name="fontFile">Font path from the style, relative to the assets root.</param>
    /// <returns>Absolute font path inside the assets root.</returns>
    /// <exception cref="InvalidDataException">The path is not an owned relative path.</exception>
    public static string Resolve(string assetsRoot, string fontFile) {
        if (assetsRoot == null || Path.IsPathRooted(fontFile) || fontFile.Contains(':')) {
            throw new InvalidDataException("Font requires an owned relative path.");
        }
        string root = Path.GetFullPath(assetsRoot);
        string path = Path.GetFullPath(Path.Combine(root, fontFile));
        if (!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException("Font escaped its assets root.");
        }
        for (string current = path; current != null; current = Path.GetDirectoryName(current)) {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) {
                throw new InvalidDataException("Font cannot traverse reparse points.");
            }
            if (string.Equals(current, root, StringComparison.OrdinalIgnoreCase)) {
                break;
            }
        }
        return path;
    }
}
