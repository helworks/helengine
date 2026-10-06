using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;

namespace helengine.editor {
    /// <summary>
    /// Reuses the scene packager's immutable outputs across disposable build workspaces.
    /// </summary>
    internal sealed class EditorScenePackageCache {
        /// <summary>
        /// Project root used to find authored inputs and the persistent build cache.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Root containing completed scene-package entries.
        /// </summary>
        readonly string CacheRootPath;

        /// <summary>
        /// Creates a cache for one project.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root.</param>
        public EditorScenePackageCache(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            CacheRootPath = Path.Combine(ProjectRootPath, "cache", "build", "scene-package", "v1");
        }

        /// <summary>
        /// Hashes the authored project snapshot and every option that changes scene packaging.
        /// </summary>
        /// <param name="platformDefinition">Selected platform definition.</param>
        /// <param name="materialBuilder">Builder used to cook material assets.</param>
        /// <param name="buildProfileId">Selected build profile.</param>
        /// <param name="graphicsProfileId">Selected graphics profile.</param>
        /// <param name="environmentId">Selected environment.</param>
        /// <param name="sceneIds">Canonical scene identities in build order.</param>
        /// <param name="sceneSourcePaths">Authored scene paths in build order.</param>
        /// <param name="importers">Registered asset importers.</param>
        /// <param name="defaultFontAsset">Default font used when scenes reference it.</param>
        /// <param name="scriptTypeResolver">Resolver used by scene component packaging.</param>
        /// <param name="shaderLibrary">Built-in shader sources used by material packaging.</param>
        /// <returns>Content-addressed cache key.</returns>
        public string ComputeKey(
            PlatformDefinition platformDefinition,
            IPlatformAssetBuilder materialBuilder,
            string buildProfileId,
            string graphicsProfileId,
            string environmentId,
            IReadOnlyList<string> sceneIds,
            IReadOnlyList<string> sceneSourcePaths,
            IReadOnlyList<IAssetImporterRegistration> importers,
            FontAsset defaultFontAsset,
            IScriptTypeResolver scriptTypeResolver,
            EditorBuiltInShaderAssetLibrary shaderLibrary) {
            if (platformDefinition == null) {
                throw new ArgumentNullException(nameof(platformDefinition));
            } else if (sceneIds == null) {
                throw new ArgumentNullException(nameof(sceneIds));
            } else if (sceneSourcePaths == null) {
                throw new ArgumentNullException(nameof(sceneSourcePaths));
            } else if (sceneIds.Count != sceneSourcePaths.Count) {
                throw new ArgumentException("Scene identities and sources must have the same count.", nameof(sceneSourcePaths));
            } else if (importers == null) {
                throw new ArgumentNullException(nameof(importers));
            }

            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            AppendText(hash, "scene-package-v1");
            AppendText(hash, JsonSerializer.Serialize(platformDefinition));
            AppendText(hash, buildProfileId ?? string.Empty);
            AppendText(hash, graphicsProfileId ?? string.Empty);
            AppendText(hash, environmentId ?? string.Empty);
            AppendType(hash, typeof(EditorPlatformBuildScenePackager));
            AppendType(hash, materialBuilder?.GetType());
            AppendType(hash, scriptTypeResolver?.GetType());
            AppendType(hash, shaderLibrary?.GetType());
            for (int index = 0; index < importers.Count; index++) {
                AppendType(hash, importers[index]?.GetType());
            }

            if (defaultFontAsset == null) {
                AppendText(hash, "no-default-font");
            } else {
                using MemoryStream fontStream = new();
                FontAssetBinarySerializer.Serialize(fontStream, defaultFontAsset);
                AppendText(hash, Convert.ToHexString(SHA256.HashData(fontStream.ToArray())));
            }

            AppendDirectory(hash, "assets", skipTemporaryGeneratedScenes: true);
            AppendDirectory(hash, "settings", skipTemporaryGeneratedScenes: false);
            AppendDirectory(hash, "helenui", skipTemporaryGeneratedScenes: false);
            AppendDirectory(hash, "src", skipTemporaryGeneratedScenes: false);
            AppendDirectory(hash, "scripts", skipTemporaryGeneratedScenes: false);
            AppendDirectory(hash, "code", skipTemporaryGeneratedScenes: false);
            AppendFile(hash, "project.heproj");
            AppendExternalDirectory(hash, Path.Combine(AppContext.BaseDirectory, "shaders", "builtin"), "packaged-built-in-shaders");
            try {
                string engineRootPath = new EditorSourceBuildWorkspaceLocator().ResolveHelEngineRootPath();
                AppendExternalDirectory(hash,
                    Path.Combine(engineRootPath, "engine", "helengine.editor", "shaders", "builtin"),
                    "source-built-in-shaders");
            } catch (InvalidOperationException) {
                // Installed editors do not have a source checkout to fingerprint.
            }

            string assetsRootPath = Path.GetFullPath(Path.Combine(ProjectRootPath, "assets"));
            string assetsRootPrefix = EnsureTrailingSeparator(assetsRootPath);
            AssetFileHasher fileHasher = new(ProjectRootPath);
            for (int index = 0; index < sceneIds.Count; index++) {
                AppendText(hash, sceneIds[index]);
                string fullScenePath = Path.GetFullPath(Path.Combine(assetsRootPath, sceneSourcePaths[index]));
                if (!fullScenePath.StartsWith(assetsRootPrefix, StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidOperationException($"Scene source '{sceneSourcePaths[index]}' is outside the project assets root.");
                }
                AppendText(hash, fileHasher.ComputeHash(fullScenePath));
            }

            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }

        /// <summary>
        /// Restores one validated scene package into a fresh execution root.
        /// </summary>
        /// <param name="key">Content-addressed package key.</param>
        /// <param name="executionRootPath">Current build execution root.</param>
        /// <param name="result">Restored package metadata when the entry exists.</param>
        /// <returns>True when a complete cache entry was restored.</returns>
        public bool TryRestore(string key, string executionRootPath, out EditorPlatformBuildScenePackagerResult result) {
            ValidateKeyAndRoot(key, executionRootPath);
            string entryRootPath = Path.Combine(CacheRootPath, key);
            result = null;
            if (!Directory.Exists(entryRootPath)) {
                return false;
            }

            string sourceRootPath = File.ReadAllText(Path.Combine(entryRootPath, "source-root.txt"));
            string resultJson = File.ReadAllText(Path.Combine(entryRootPath, "result.json"));
            EditorPlatformBuildScenePackagerResult cachedResult = JsonSerializer.Deserialize<EditorPlatformBuildScenePackagerResult>(resultJson)
                ?? throw new InvalidDataException($"Scene package cache '{key}' has no result metadata.");
            Dictionary<string, string> fileHashes = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(Path.Combine(entryRootPath, "files.json")))
                ?? throw new InvalidDataException($"Scene package cache '{key}' has no file manifest.");
            string payloadRootPath = Path.Combine(entryRootPath, "payload");
            foreach (KeyValuePair<string, string> fileEntry in fileHashes) {
                string relativePath = ValidatePayloadPath(fileEntry.Key);
                string payloadPath = Path.Combine(payloadRootPath, relativePath);
                if (!File.Exists(payloadPath) || !string.Equals(ComputeFileHash(payloadPath), fileEntry.Value, StringComparison.Ordinal)) {
                    throw new InvalidDataException($"Scene package cache '{key}' contains a missing or modified file '{relativePath}'.");
                }
            }

            foreach (string relativePath in fileHashes.Keys) {
                string sourcePath = Path.Combine(payloadRootPath, relativePath);
                string destinationPath = Path.Combine(executionRootPath, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)
                    ?? throw new InvalidOperationException("Cached package output has no parent directory."));
                File.Copy(sourcePath, destinationPath, true);
            }

            PlatformCookWorkItem[] workItems = cachedResult.PlatformCookWorkItems
                .Select(item => RebaseWorkItem(item, sourceRootPath, executionRootPath))
                .ToArray();
            result = new EditorPlatformBuildScenePackagerResult(
                cachedResult.ReferencedShaderDependencies,
                workItems,
                cachedResult.CookedArtifactDeclarations);
            return true;
        }

        /// <summary>
        /// Publishes the packager-owned cooked and generated files as one complete entry.
        /// </summary>
        /// <param name="key">Content-addressed package key.</param>
        /// <param name="executionRootPath">Execution root holding freshly packaged scenes.</param>
        /// <param name="result">Fresh package metadata.</param>
        public void Store(string key, string executionRootPath, EditorPlatformBuildScenePackagerResult result) {
            ValidateKeyAndRoot(key, executionRootPath);
            if (result == null) {
                throw new ArgumentNullException(nameof(result));
            }

            Directory.CreateDirectory(CacheRootPath);
            string entryRootPath = Path.Combine(CacheRootPath, key);
            if (Directory.Exists(entryRootPath)) {
                return;
            }

            string stagingRootPath = Path.Combine(CacheRootPath, key + "." + Guid.NewGuid().ToString("N") + ".tmp");
            string payloadRootPath = Path.Combine(stagingRootPath, "payload");
            Directory.CreateDirectory(payloadRootPath);
            try {
                Dictionary<string, string> fileHashes = new(StringComparer.Ordinal);
                StoreDirectory("cooked", executionRootPath, payloadRootPath, fileHashes);
                StoreDirectory("generated", executionRootPath, payloadRootPath, fileHashes);
                File.WriteAllText(Path.Combine(stagingRootPath, "source-root.txt"), Path.GetFullPath(executionRootPath));
                File.WriteAllText(Path.Combine(stagingRootPath, "result.json"), JsonSerializer.Serialize(result));
                File.WriteAllText(Path.Combine(stagingRootPath, "files.json"), JsonSerializer.Serialize(fileHashes));
                if (!Directory.Exists(entryRootPath)) {
                    try {
                        Directory.Move(stagingRootPath, entryRootPath);
                    } catch (IOException) when (Directory.Exists(entryRootPath)) {
                        // Another build published the same immutable entry first.
                    }
                }
            } finally {
                if (Directory.Exists(stagingRootPath)) {
                    Directory.Delete(stagingRootPath, true);
                }
            }
        }

        /// <summary>
        /// Adds stable authored file names and content hashes from one project directory.
        /// </summary>
        /// <param name="hash">Aggregate input hash.</param>
        /// <param name="relativeDirectoryPath">Project-relative directory to scan.</param>
        /// <param name="skipTemporaryGeneratedScenes">Whether queue-specific temporary scenes should be omitted.</param>
        void AppendDirectory(IncrementalHash hash, string relativeDirectoryPath, bool skipTemporaryGeneratedScenes) {
            string rootPath = Path.Combine(ProjectRootPath, relativeDirectoryPath);
            if (!Directory.Exists(rootPath)) {
                return;
            }

            string[] files = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            AssetFileHasher fileHasher = new(ProjectRootPath);
            foreach (string filePath in files) {
                string relativePath = Path.GetRelativePath(rootPath, filePath).Replace('\\', '/');
                if (skipTemporaryGeneratedScenes && relativePath.StartsWith(".generated-build/", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                AppendText(hash, relativeDirectoryPath + "/" + relativePath);
                AppendText(hash, fileHasher.ComputeHash(filePath));
            }
        }

        /// <summary>
        /// Adds one optional project file to the aggregate input hash.
        /// </summary>
        /// <param name="hash">Aggregate input hash.</param>
        /// <param name="relativePath">Project-relative file.</param>
        void AppendFile(IncrementalHash hash, string relativePath) {
            string fullPath = Path.Combine(ProjectRootPath, relativePath);
            if (File.Exists(fullPath)) {
                AppendText(hash, relativePath);
                AppendText(hash, new AssetFileHasher(ProjectRootPath).ComputeHash(fullPath));
            }
        }

        /// <summary>
        /// Adds external built-in shader source contents that can change without an editor assembly rebuild.
        /// </summary>
        /// <param name="hash">Aggregate input hash.</param>
        /// <param name="rootPath">Absolute shader source directory.</param>
        /// <param name="rootId">Stable identity for this source directory.</param>
        static void AppendExternalDirectory(IncrementalHash hash, string rootPath, string rootId) {
            if (!Directory.Exists(rootPath)) {
                return;
            }

            string[] files = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string filePath in files) {
                AppendText(hash, rootId + "/" + Path.GetRelativePath(rootPath, filePath).Replace('\\', '/'));
                AppendText(hash, ComputeFileHash(filePath));
            }
        }

        /// <summary>
        /// Copies all files from one packager-owned directory into the staged entry.
        /// </summary>
        /// <param name="directoryName">Packager-owned directory name.</param>
        /// <param name="executionRootPath">Current execution root.</param>
        /// <param name="payloadRootPath">Staged payload root.</param>
        /// <param name="fileHashes">Manifest receiving every stored file hash.</param>
        static void StoreDirectory(string directoryName, string executionRootPath, string payloadRootPath, IDictionary<string, string> fileHashes) {
            string directoryPath = Path.Combine(executionRootPath, directoryName);
            if (!Directory.Exists(directoryPath)) {
                return;
            }

            foreach (string sourcePath in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories)) {
                string relativePath = Path.GetRelativePath(executionRootPath, sourcePath).Replace('\\', '/');
                string payloadPath = Path.Combine(payloadRootPath, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(payloadPath)
                    ?? throw new InvalidOperationException("Cached package payload has no parent directory."));
                File.Copy(sourcePath, payloadPath);
                fileHashes.Add(relativePath, ComputeFileHash(payloadPath));
            }
        }

        /// <summary>
        /// Replaces a prior disposable-workspace source path with the current execution root.
        /// </summary>
        /// <param name="item">Cached platform cook work item.</param>
        /// <param name="oldRootPath">Execution root used to create the cache entry.</param>
        /// <param name="newRootPath">Current execution root.</param>
        /// <returns>Work item whose source points to a file in the current workspace.</returns>
        static PlatformCookWorkItem RebaseWorkItem(PlatformCookWorkItem item, string oldRootPath, string newRootPath) {
            string oldRootPrefix = EnsureTrailingSeparator(Path.GetFullPath(oldRootPath));
            string sourcePath = Path.GetFullPath(item.SourceAssetPath);
            if (sourcePath.StartsWith(oldRootPrefix, StringComparison.OrdinalIgnoreCase)) {
                sourcePath = Path.Combine(newRootPath, Path.GetRelativePath(oldRootPath, sourcePath));
                if (!File.Exists(sourcePath)) {
                    throw new InvalidDataException($"Cached work item source '{sourcePath}' was not restored.");
                }
            }

            return new PlatformCookWorkItem(
                item.WorkItemId,
                sourcePath,
                item.SourceAssetKind,
                item.TargetPlatformId,
                item.TargetArtifactKind,
                item.OutputRelativePath,
                item.OutputLogicalArtifactId,
                item.SourceContentHash,
                item.SettingsHash,
                item.SerializedPlatformSettings,
                item.Metadata);
        }

        /// <summary>
        /// Prevents a persisted relative file name from escaping its payload root.
        /// </summary>
        /// <param name="relativePath">File name read from the cache manifest.</param>
        /// <returns>Validated relative file name.</returns>
        static string ValidatePayloadPath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)
                || Path.IsPathRooted(relativePath)
                || relativePath.Split('/', '\\').Contains("..", StringComparer.Ordinal)
                || !(relativePath.StartsWith("cooked/", StringComparison.Ordinal)
                    || relativePath.StartsWith("generated/", StringComparison.Ordinal))) {
                throw new InvalidDataException($"Scene package cache contains an invalid payload path '{relativePath}'.");
            }
            return relativePath;
        }

        /// <summary>
        /// Rejects malformed cache keys and missing execution roots before file access.
        /// </summary>
        /// <param name="key">Content-addressed key.</param>
        /// <param name="executionRootPath">Current execution root.</param>
        static void ValidateKeyAndRoot(string key, string executionRootPath) {
            if (string.IsNullOrWhiteSpace(key) || key.Length != 64 || key.Any(character => !Uri.IsHexDigit(character))) {
                throw new ArgumentException("A SHA-256 scene package key is required.", nameof(key));
            } else if (string.IsNullOrWhiteSpace(executionRootPath)) {
                throw new ArgumentException("Execution root path must be provided.", nameof(executionRootPath));
            }
        }

        /// <summary>
        /// Adds a type's module identity so code changes invalidate cached scene outputs.
        /// </summary>
        /// <param name="hash">Aggregate input hash.</param>
        /// <param name="type">Implementation type, when present.</param>
        static void AppendType(IncrementalHash hash, Type type) {
            AppendText(hash, type == null
                ? "none"
                : type.FullName + ":" + type.Assembly.ManifestModule.ModuleVersionId.ToString("N"));
        }

        /// <summary>
        /// Adds one length-delimited string to the aggregate input hash.
        /// </summary>
        /// <param name="hash">Aggregate input hash.</param>
        /// <param name="value">Value to append.</param>
        static void AppendText(IncrementalHash hash, string value) {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            hash.AppendData(BitConverter.GetBytes(bytes.Length));
            hash.AppendData(bytes);
        }

        /// <summary>
        /// Computes the integrity hash of one cached payload file.
        /// </summary>
        /// <param name="path">Absolute file path.</param>
        /// <returns>Lowercase hexadecimal SHA-256 hash.</returns>
        static string ComputeFileHash(string path) {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        /// <summary>
        /// Produces a directory prefix suitable for containment comparisons.
        /// </summary>
        /// <param name="path">Canonical directory path.</param>
        /// <returns>Path with a trailing separator.</returns>
        static string EnsureTrailingSeparator(string path) {
            return Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;
        }
    }
}
