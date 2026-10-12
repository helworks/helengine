namespace helengine {
    /// <summary>
    /// Describes one built runtime scene entry that can be loaded by scene id.
    /// </summary>
    public sealed class RuntimeSceneCatalogEntry : IDisposable {
        /// <summary>Owned copy of direct module ids, released when the owning catalog entry is disposed.</summary>
        [NativeOwnedMember]
        string[] RequiredCodeModuleIdsValue;
        /// <summary>
        /// Initializes one runtime scene catalog entry.
        /// </summary>
        /// <param name="sceneId">Stable built scene id.</param>
        /// <param name="cookedRelativePath">Resolved runtime scene payload path, either as a canonical cooked-relative path or as an already-rooted platform runtime path.</param>
        public RuntimeSceneCatalogEntry(string sceneId, string cookedRelativePath)
            : this(sceneId, cookedRelativePath, Array.Empty<string>()) {
        }

        /// <summary>Initializes a scene entry with the exact native code modules required before component factories can materialize it.</summary>
        /// <param name="sceneId">Stable packaged scene identity.</param>
        /// <param name="cookedRelativePath">Validated runtime content path.</param>
        /// <param name="requiredCodeModuleIds">Borrowed direct module ids copied into the immutable entry; dependencies are expanded and validated by the module manager.</param>
        public RuntimeSceneCatalogEntry(string sceneId, string cookedRelativePath, [NativeNoEscape] string[] requiredCodeModuleIds) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }
            if (string.IsNullOrWhiteSpace(cookedRelativePath)) {
                throw new ArgumentException("Cooked relative path is required.", nameof(cookedRelativePath));
            }
            if (requiredCodeModuleIds == null) {
                throw new ArgumentNullException(nameof(requiredCodeModuleIds));
            }
            for (int index = 0; index < requiredCodeModuleIds.Length; index++) {
                if (string.IsNullOrWhiteSpace(requiredCodeModuleIds[index])) {
                    throw new ArgumentException("Required code module ids cannot be blank.", nameof(requiredCodeModuleIds));
                }
            }

            SceneId = sceneId;
            CookedRelativePath = NormalizeCookedRelativePath(cookedRelativePath);
            string[] copiedIds = new string[requiredCodeModuleIds.Length];
            for (int index = 0; index < copiedIds.Length; index++) { copiedIds[index] = requiredCodeModuleIds[index]; }
            RequiredCodeModuleIdsValue = copiedIds;
        }

        /// <summary>
        /// Gets the stable built scene id.
        /// </summary>
        public string SceneId { get; }

        /// <summary>
        /// Gets the resolved runtime scene payload path.
        /// </summary>
        public string CookedRelativePath { get; }

        /// <summary>Gets entry-owned direct native code dependencies declared by the packaged scene catalog; callers borrow this array.</summary>
        public string[] RequiredCodeModuleIds => RequiredCodeModuleIdsValue;

        /// <summary>Releases copied dependency metadata after all scene loading and code acquisition operations have ended.</summary>
        public void Dispose() {
            NativeOwnership.Release(ref RequiredCodeModuleIdsValue);
        }

        /// <summary>
        /// Validates one runtime scene path according to the active runtime-path contract.
        /// </summary>
        /// <param name="cookedRelativePath">Runtime scene path to validate.</param>
        /// <returns>The original rooted runtime path, or the canonical cooked-relative path for content-relative runtimes.</returns>
        static string NormalizeCookedRelativePath(string cookedRelativePath) {
            if (IsRootedRuntimePath(cookedRelativePath)) {
                return cookedRelativePath;
            }

            return CanonicalPackagedAssetPath.ValidateCanonical(cookedRelativePath);
        }

        /// <summary>
        /// Determines whether one runtime scene path is already rooted to a platform-specific runtime device or filesystem location.
        /// </summary>
        /// <param name="path">Runtime scene path to inspect.</param>
        /// <returns>True when the path is already rooted; otherwise false.</returns>
        static bool IsRootedRuntimePath(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return false;
            }

            if (path[0] == '/' || path[0] == '\\') {
                return true;
            }
            if (path.Length >= 2 && path[1] == ':') {
                return true;
            }

            for (int index = 1; index < path.Length - 1; index++) {
                if (path[index] != ':') {
                    continue;
                }

                char nextCharacter = path[index + 1];
                return nextCharacter == '/' || nextCharacter == '\\';
            }

            return false;
        }
    }
}
