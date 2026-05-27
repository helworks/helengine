using System.Text;

namespace helengine;

/// <summary>
/// Normalizes and validates canonical packaged and runtime asset paths shared across every platform contract.
/// </summary>
public static class CanonicalPackagedAssetPath {
    /// <summary>
    /// Normalizes one logical packaged asset path into lowercase forward-slash form after rejecting rooted and traversal paths.
    /// </summary>
    /// <param name="path">Logical packaged or runtime asset path to normalize.</param>
    /// <returns>Canonical lowercase path that always uses forward slashes.</returns>
    public static string Normalize(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Packaged asset path must be provided.", nameof(path));
        }

        string normalizedPath = NormalizeSlashAndCase(path.Trim());
        if (IsRootedPath(normalizedPath)) {
            throw new InvalidOperationException($"Packaged asset path '{path}' must be relative.");
        }

        string canonicalPath = CollapseAndValidateSegments(normalizedPath, path);
        if (canonicalPath.Length == 0) {
            throw new InvalidOperationException($"Packaged asset path '{path}' must not be empty.");
        }

        return canonicalPath;
    }

    /// <summary>
    /// Validates that one logical packaged asset path is already in canonical lowercase forward-slash form.
    /// </summary>
    /// <param name="path">Logical packaged or runtime asset path to validate.</param>
    /// <returns>The original path when it is already canonical.</returns>
    public static string ValidateCanonical(string path) {
        string normalizedPath = Normalize(path);
        if (!string.Equals(normalizedPath, path, StringComparison.Ordinal)) {
            throw new InvalidOperationException($"Packaged asset path '{path}' is not canonical. Expected '{normalizedPath}'.");
        }

        return normalizedPath;
    }

    /// <summary>
    /// Rewrites slash direction and character casing into the canonical packaged-path alphabet used by runtime content.
    /// </summary>
    /// <param name="path">Logical packaged or runtime asset path to rewrite.</param>
    /// <returns>Path that uses forward slashes and lowercase characters.</returns>
    static string NormalizeSlashAndCase(string path) {
        string lowercasePath = path.ToLowerInvariant();
        StringBuilder builder = new StringBuilder(lowercasePath.Length);
        for (int index = 0; index < lowercasePath.Length; index++) {
            char character = lowercasePath[index];
            if (character == '\\') {
                builder.Append('/');
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Determines whether one logical packaged path is rooted instead of content-relative.
    /// </summary>
    /// <param name="path">Normalized packaged path to inspect.</param>
    /// <returns>True when the path is rooted; otherwise false.</returns>
    static bool IsRootedPath(string path) {
        if (path.Length == 0) {
            return false;
        }

        if (path[0] == '/') {
            return true;
        }

        return path.Length >= 2 && path[1] == ':';
    }

    /// <summary>
    /// Collapses duplicate separators and rejects traversal segments from one normalized packaged path.
    /// </summary>
    /// <param name="normalizedPath">Slash-normalized packaged path to validate.</param>
    /// <param name="originalPath">Original caller-supplied packaged path used for diagnostics.</param>
    /// <returns>Canonical packaged path with duplicate separators removed.</returns>
    static string CollapseAndValidateSegments(string normalizedPath, string originalPath) {
        StringBuilder builder = new StringBuilder(normalizedPath.Length);
        bool wroteSegment = false;
        int index = 0;
        while (index < normalizedPath.Length) {
            while (index < normalizedPath.Length && normalizedPath[index] == '/') {
                index++;
            }

            if (index >= normalizedPath.Length) {
                break;
            }

            int segmentStart = index;
            while (index < normalizedPath.Length && normalizedPath[index] != '/') {
                index++;
            }

            string segment = normalizedPath.Substring(segmentStart, index - segmentStart);
            if (segment == "." || segment == "..") {
                throw new InvalidOperationException($"Packaged asset path '{originalPath}' must not contain traversal segments.");
            }

            if (wroteSegment) {
                builder.Append('/');
            }

            builder.Append(segment);
            wroteSegment = true;
        }

        return builder.ToString();
    }
}
