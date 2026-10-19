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
        /// File remembering the content hash of each key input by its length and last-write stamp.
        /// </summary>
        readonly string InputStampMemoPath;

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
            InputStampMemoPath = Path.Combine(ProjectRootPath, "cache", "build", "scene-package", EditorScenePackageInputStampMemo.FileName);
        }

        /// <summary>
        /// Number of project input files the latest <see cref="ComputeKey"/> had to hash because they were new or
        /// their stamp changed.
        /// </summary>
        public int LastHashedInputCount { get; private set; }

        /// <summary>
        /// Number of project input files the latest <see cref="ComputeKey"/> took from the stamp memo.
        /// </summary>
        public int LastReusedInputCount { get; private set; }

        /// <summary>Produces a stable versioned identity for the backend's effective layout metrics.</summary>
        /// <param name="target">Resolved backend output, or null when unavailable.</param>
        /// <param name="algorithmVersion">Version of the numerical layout and ownership contract.</param>
        /// <returns>Invariant fingerprint input, independent of profile display names.</returns>
        internal static string ComputeLayoutFingerprint(PlatformLayoutTarget target, int algorithmVersion) {
            return algorithmVersion.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n" + JsonSerializer.Serialize(target);
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
        /// <returns>Content-addressed cache key with the content hash of every file it covers.</returns>
        public EditorScenePackageKey ComputeKey(
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
            EditorBuiltInShaderAssetLibrary shaderLibrary,
            PlatformLayoutTarget effectiveLayoutTarget = null,
            NumericPrecisionPolicy numericPolicy = null) {
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
            AppendText(hash, "scene-package-v9-numeric-policy");
            AppendText(hash, (numericPolicy ?? NumericPrecisionPolicy.Default).Identity);
            AppendText(hash, ComputeLayoutFingerprint(effectiveLayoutTarget, ViewportLayoutCookProcessor.AlgorithmVersion));
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

            // One read batch verifies and pins each assets directory chain once for the whole key instead of once
            // per hashed file; every leaf is still checked and opened through a verified handle.
            using EditorAuthoringReadBatch readBatch = EditorAuthoringReadBatch.Begin(ProjectRootPath);
            EditorScenePackageInputStampMemo stampMemo = EditorScenePackageInputStampMemo.Begin(InputStampMemoPath);
            AssetFileHasher fileHasher = new(ProjectRootPath);
            Dictionary<string, string> inputHashes = new(StringComparer.Ordinal);
            AppendDirectory(hash, "assets", skipTemporaryGeneratedScenes: true, stampMemo, fileHasher, inputHashes);
            AppendDirectory(hash, "settings", skipTemporaryGeneratedScenes: false, stampMemo, fileHasher, inputHashes);
            AppendDirectory(hash, "helenui", skipTemporaryGeneratedScenes: false, stampMemo, fileHasher, inputHashes);
            AppendDirectory(hash, "src", skipTemporaryGeneratedScenes: false, stampMemo, fileHasher, inputHashes);
            AppendDirectory(hash, "scripts", skipTemporaryGeneratedScenes: false, stampMemo, fileHasher, inputHashes);
            AppendDirectory(hash, "code", skipTemporaryGeneratedScenes: false, stampMemo, fileHasher, inputHashes);
            AppendFile(hash, "project.heproj", stampMemo, fileHasher, inputHashes);
            AppendExternalDirectory(hash, Path.Combine(AppContext.BaseDirectory, "shaders", "builtin"), "packaged-built-in-shaders", inputHashes);
            try {
                string engineRootPath = new EditorSourceBuildWorkspaceLocator().ResolveHelEngineRootPath();
                AppendExternalDirectory(hash,
                    Path.Combine(engineRootPath, "engine", "helengine.editor", "shaders", "builtin"),
                    "source-built-in-shaders",
                    inputHashes);
            } catch (InvalidOperationException) {
                // Installed editors do not have a source checkout to fingerprint.
            }

            string assetsRootPath = Path.GetFullPath(Path.Combine(ProjectRootPath, "assets"));
            string assetsRootPrefix = EnsureTrailingSeparator(assetsRootPath);
            for (int index = 0; index < sceneIds.Count; index++) {
                AppendText(hash, sceneIds[index]);
                string fullScenePath = Path.GetFullPath(Path.Combine(assetsRootPath, sceneSourcePaths[index]));
                if (!fullScenePath.StartsWith(assetsRootPrefix, StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidOperationException($"Scene source '{sceneSourcePaths[index]}' is outside the project assets root.");
                }

                // Scenes under the assets tree were hashed by the directory pass above; only temporary generated
                // scenes, which that pass skips, are hashed here.
                string sceneInputPath = "assets/" + Path.GetRelativePath(assetsRootPath, fullScenePath).Replace('\\', '/');
                if (!inputHashes.TryGetValue(sceneInputPath, out string sceneHash)) {
                    sceneHash = fileHasher.ComputeHash(fullScenePath);
                    inputHashes[sceneInputPath] = sceneHash;
                }
                AppendText(hash, sceneHash);
            }

            stampMemo.Save();
            LastHashedInputCount = stampMemo.HashedFileCount;
            LastReusedInputCount = stampMemo.ReusedFileCount;
            return new EditorScenePackageKey(Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), inputHashes);
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
            HashSet<string> selectedRuntimePaths = cachedResult.SceneCookedDependencies.Values.SelectMany(paths => paths).ToHashSet(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> fileEntry in fileHashes) {
                string relativePath = ValidatePayloadPath(fileEntry.Key, selectedRuntimePaths);
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
                cachedResult.CookedArtifactDeclarations,
                cachedResult.SceneCookedDependencies);
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
                StoreSelectedRuntimeDependencies(executionRootPath, payloadRootPath, result.SceneCookedDependencies, fileHashes);
                File.WriteAllText(Path.Combine(stagingRootPath, "source-root.txt"), Path.GetFullPath(executionRootPath));
                File.WriteAllText(Path.Combine(stagingRootPath, "result.json"), JsonSerializer.Serialize(result));
                File.WriteAllText(Path.Combine(stagingRootPath, "files.json"), JsonSerializer.Serialize(fileHashes));
                if (!Directory.Exists(entryRootPath)) {
                    try {
                        EditorDirectoryPublicationMove.Move(stagingRootPath, entryRootPath);
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
        /// <para>
        /// One listing supplies every file's length and last-write stamp, and the stamp memo decides which files
        /// must be hashed. Files that are hashed are read through <see cref="AssetFileHasher"/>, which verifies and
        /// pins the directory chain (once per directory inside the caller's read batch) and verifies the leaf
        /// handle, exactly as before. A file whose remembered hash is reused is not opened at all, so no byte is read
        /// without that verification. Reparse points are still rejected for every hashed input: the listing reports
        /// each directory's and file's attributes, so a reparse-point leaf, or a file beneath a reparse-point
        /// directory, fails the key just as the per-file chain validation did, and the root's own chain is
        /// validated once.
        /// </para>
        /// </summary>
        /// <param name="hash">Aggregate input hash.</param>
        /// <param name="relativeDirectoryPath">Project-relative directory to scan.</param>
        /// <param name="skipTemporaryGeneratedScenes">Whether queue-specific temporary scenes should be omitted.</param>
        /// <param name="stampMemo">Memo pass supplying remembered hashes for unchanged files.</param>
        /// <param name="fileHasher">Verified hasher for new or changed files.</param>
        /// <param name="inputHashes">Receives each appended file's content hash by its appended path.</param>
        void AppendDirectory(
            IncrementalHash hash,
            string relativeDirectoryPath,
            bool skipTemporaryGeneratedScenes,
            EditorScenePackageInputStampMemo stampMemo,
            AssetFileHasher fileHasher,
            Dictionary<string, string> inputHashes) {
            string rootPath = Path.Combine(ProjectRootPath, relativeDirectoryPath);
            if (!Directory.Exists(rootPath)) {
                return;
            }

            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(ProjectRootPath, ProjectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(rootPath, ProjectRootPath);
            List<FileInfo> files = new();
            List<string> reparseDirectoryPrefixes = new();
            foreach (FileSystemInfo entry in new DirectoryInfo(rootPath).EnumerateFileSystemInfos("*", SearchOption.AllDirectories)) {
                if (entry is FileInfo file) {
                    files.Add(file);
                } else if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) {
                    reparseDirectoryPrefixes.Add(EnsureTrailingSeparator(entry.FullName));
                }
            }

            files.Sort((left, right) => string.CompareOrdinal(left.FullName, right.FullName));
            foreach (FileInfo file in files) {
                string relativePath = Path.GetRelativePath(rootPath, file.FullName).Replace('\\', '/');
                if (skipTemporaryGeneratedScenes && relativePath.StartsWith(".generated-build/", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                RejectReparseTraversal(file, reparseDirectoryPrefixes);
                string inputPath = relativeDirectoryPath + "/" + relativePath;
                string contentHash = stampMemo.ResolveHash(inputPath, file, fileHasher);
                AppendText(hash, inputPath);
                AppendText(hash, contentHash);
                inputHashes[inputPath] = contentHash;
            }
        }

        /// <summary>
        /// Adds one optional project file to the aggregate input hash.
        /// </summary>
        /// <param name="hash">Aggregate input hash.</param>
        /// <param name="relativePath">Project-relative file.</param>
        /// <param name="stampMemo">Memo pass supplying the remembered hash when the file is unchanged.</param>
        /// <param name="fileHasher">Verified hasher used when the file changed.</param>
        /// <param name="inputHashes">Receives the file's content hash by its appended path.</param>
        void AppendFile(
            IncrementalHash hash,
            string relativePath,
            EditorScenePackageInputStampMemo stampMemo,
            AssetFileHasher fileHasher,
            Dictionary<string, string> inputHashes) {
            FileInfo file = new(Path.Combine(ProjectRootPath, relativePath));
            if (file.Exists) {
                string contentHash = stampMemo.ResolveHash(relativePath, file, fileHasher);
                AppendText(hash, relativePath);
                AppendText(hash, contentHash);
                inputHashes[relativePath] = contentHash;
            }
        }

        /// <summary>
        /// Rejects a listed input that is itself a reparse point or lies beneath a reparse-point directory, matching
        /// the verified read path, which refuses to traverse either.
        /// </summary>
        /// <param name="file">Listed input file.</param>
        /// <param name="reparseDirectoryPrefixes">Reparse-point directories found by the same listing, with trailing separators.</param>
        static void RejectReparseTraversal(FileInfo file, List<string> reparseDirectoryPrefixes) {
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0) {
                throw new InvalidDataException($"The scene-package input '{file.FullName}' is a reparse point.");
            }

            for (int index = 0; index < reparseDirectoryPrefixes.Count; index++) {
                if (file.FullName.StartsWith(reparseDirectoryPrefixes[index], StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidDataException($"The scene-package input '{file.FullName}' traverses a reparse point.");
                }
            }
        }

        /// <summary>
        /// Adds external built-in shader source contents that can change without an editor assembly rebuild.
        /// </summary>
        /// <param name="hash">Aggregate input hash.</param>
        /// <param name="rootPath">Absolute shader source directory.</param>
        /// <param name="rootId">Stable identity for this source directory.</param>
        /// <param name="inputHashes">Receives each file's content hash by its appended identity.</param>
        static void AppendExternalDirectory(IncrementalHash hash, string rootPath, string rootId, Dictionary<string, string> inputHashes) {
            if (!Directory.Exists(rootPath)) {
                return;
            }

            string[] files = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string filePath in files) {
                string inputPath = rootId + "/" + Path.GetRelativePath(rootPath, filePath).Replace('\\', '/');
                string contentHash = ComputeFileHash(filePath);
                AppendText(hash, inputPath);
                AppendText(hash, contentHash);
                inputHashes[inputPath] = contentHash;
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

        /// <summary>Preserves only selected materialized runtime files outside the standard cook directories, such as a legacy font copy.</summary>
        /// <param name="executionRootPath">Fresh package execution root.</param>
        /// <param name="payloadRootPath">Cache payload root receiving selected files.</param>
        /// <param name="sceneDependencies">Authoritative rewritten scene dependency paths.</param>
        /// <param name="fileHashes">Manifest receiving copied file identities and hashes.</param>
        static void StoreSelectedRuntimeDependencies(string executionRootPath, string payloadRootPath,
            IReadOnlyDictionary<string, string[]> sceneDependencies, IDictionary<string, string> fileHashes) {
            HashSet<string> selectedRuntimePaths = sceneDependencies.Values.SelectMany(paths => paths).ToHashSet(StringComparer.Ordinal);
            foreach (string selectedPath in selectedRuntimePaths.OrderBy(path => path, StringComparer.Ordinal)) {
                string relativePath = ValidatePayloadPath(selectedPath, selectedRuntimePaths);
                if (relativePath.StartsWith("cooked/", StringComparison.Ordinal) || relativePath.StartsWith("generated/", StringComparison.Ordinal)) continue;
                string sourcePath = Path.Combine(executionRootPath, relativePath);
                // Builder-owned outputs may be selected before their later platform cook phase.
                // The package artifact validation remains responsible for requiring final files.
                if (!File.Exists(sourcePath)) continue;
                string destinationPath = Path.Combine(payloadRootPath, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)
                    ?? throw new InvalidOperationException("Selected cached dependency has no parent directory."));
                File.Copy(sourcePath, destinationPath);
                fileHashes.Add(relativePath, ComputeFileHash(sourcePath));
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
        /// <param name="selectedRuntimePaths">Exact additional runtime files selected by the stored scene dependencies.</param>
        /// <returns>Validated relative file name.</returns>
        static string ValidatePayloadPath(string relativePath, IReadOnlySet<string> selectedRuntimePaths = null) {
            if (string.IsNullOrWhiteSpace(relativePath)
                || Path.IsPathRooted(relativePath)
                || relativePath.Split('/', '\\').Contains("..", StringComparer.Ordinal)
                || !(relativePath.StartsWith("cooked/", StringComparison.Ordinal)
                    || relativePath.StartsWith("generated/", StringComparison.Ordinal)
                    || selectedRuntimePaths != null && selectedRuntimePaths.Contains(relativePath))) {
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
