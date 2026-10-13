namespace helengine {
    /// <summary>
    /// Packaged path policy for hosts that serve cooked content from one content root, such as the desktop player running
    /// content cooked for a console whose packaged paths are rooted (for example <c>/cooked/imported/abc</c>).
    /// A single leading <c>/</c> or <c>\</c> means "the content root", so it is stripped and the remainder must be a canonical
    /// content-relative path; already-relative paths are validated with the same canonical rules. Drive-letter and device-scheme
    /// paths point outside the content root and are rejected.
    /// </summary>
    public sealed class ContentRootPackagedAssetPathPolicy : IPackagedAssetPathPolicy {
        /// <summary>
        /// Maps one packaged asset path to its canonical path relative to the content root.
        /// </summary>
        /// <param name="packagedPath">Packaged asset path exactly as stored in cooked content.</param>
        /// <returns>Canonical content-relative path opened beneath the host content root.</returns>
        public string ResolvePackagedPath(string packagedPath) {
            if (string.IsNullOrWhiteSpace(packagedPath)) {
                throw new ArgumentException("Packaged asset path must be provided.", nameof(packagedPath));
            }
            if (ContainsDriveOrScheme(packagedPath)) {
                throw new InvalidOperationException(
                    $"Packaged asset path '{packagedPath}' names a drive or device outside the content root; only content-relative or content-root-rooted paths are supported.");
            }

            if (packagedPath[0] == '/' || packagedPath[0] == '\\') {
                return CanonicalPackagedAssetPath.ValidateCanonical(packagedPath.Substring(1));
            }

            return CanonicalPackagedAssetPath.ValidateCanonical(packagedPath);
        }

        /// <summary>
        /// Determines whether one packaged path carries a drive letter (<c>C:</c>) or device scheme (<c>cdrom0:</c>), both of which
        /// use a colon that never appears in canonical content-relative paths.
        /// </summary>
        /// <param name="path">Packaged path to inspect.</param>
        /// <returns>True when the path contains a colon; otherwise false.</returns>
        static bool ContainsDriveOrScheme(string path) {
            for (int index = 0; index < path.Length; index++) {
                if (path[index] == ':') {
                    return true;
                }
            }

            return false;
        }
    }
}
