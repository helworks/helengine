namespace helengine.patching {
    /// <summary>
    /// Builds an engine build plan from resolved patches.
    /// </summary>
    public sealed class EngineBuildPlanBuilder {
        readonly EngineBuildKey buildKey;

        /// <summary>
        /// Initializes a new build plan builder.
        /// </summary>
        public EngineBuildPlanBuilder() {
            buildKey = new EngineBuildKey();
        }

        /// <summary>
        /// Builds a plan for the provided build request and resolved patches.
        /// </summary>
        /// <param name="request">Build request.</param>
        /// <param name="resolution">Resolved patch set.</param>
        /// <returns>Build plan.</returns>
        public EngineBuildPlan Build(EngineBuildRequest request, EnginePatchResolution resolution) {
            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            }

            if (resolution == null) {
                throw new ArgumentNullException(nameof(resolution));
            }

            string engineRoot = request.EngineRootPath;
            string coreRoot = Path.Combine(engineRoot, "engine", "helengine.core");
            string assemblyName = "helengine.core";

            List<string> sourceFiles = GetSourceFiles(coreRoot);
            HashSet<string> excludeSet = BuildExcludeSet(engineRoot, resolution.Patches);
            ApplyExcludes(sourceFiles, excludeSet);

            HashSet<string> sourceSet = new HashSet<string>(sourceFiles, StringComparer.OrdinalIgnoreCase);
            AddIncludes(sourceSet, resolution.Patches);
            List<string> finalizedSources = new List<string>(sourceSet);
            SortList(finalizedSources);

            List<string> defines = BuildDefines(resolution.Patches);

            string buildId = buildKey.ComputeBuildId(request.Configuration, resolution.Patches, finalizedSources, defines);
            string buildRoot = Path.Combine(request.OutputRootPath, buildId);
            string outputPath = Path.Combine(buildRoot, "out");
            string projectPath = Path.Combine(buildRoot, "helengine.core.impl.csproj");

            return new EngineBuildPlan(
                buildId,
                buildRoot,
                projectPath,
                outputPath,
                request.Configuration,
                assemblyName,
                finalizedSources,
                defines,
                resolution.Patches);
        }

        /// <summary>
        /// Collects source files from the core engine folder.
        /// </summary>
        /// <param name="coreRoot">Core engine root folder.</param>
        /// <returns>List of source file paths.</returns>
        List<string> GetSourceFiles(string coreRoot) {
            var files = new List<string>();
            if (!Directory.Exists(coreRoot)) {
                return files;
            }

            foreach (string path in Directory.EnumerateFiles(coreRoot, "*.cs", SearchOption.AllDirectories)) {
                if (IsBuildArtifactPath(path)) {
                    continue;
                }

                files.Add(Path.GetFullPath(path));
            }

            return files;
        }

        /// <summary>
        /// Builds a set of files to exclude based on patch manifests.
        /// </summary>
        /// <param name="engineRoot">Engine root folder.</param>
        /// <param name="patches">Resolved patches.</param>
        /// <returns>Full-path exclude set.</returns>
        HashSet<string> BuildExcludeSet(string engineRoot, IReadOnlyList<EnginePatchDefinition> patches) {
            var excludeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (patches == null) {
                return excludeSet;
            }

            for (int i = 0; i < patches.Count; i++) {
                EnginePatchDefinition patch = patches[i];
                List<string> excludes = patch.Manifest.ExcludeFiles;
                if (excludes == null || excludes.Count == 0) {
                    continue;
                }

                for (int j = 0; j < excludes.Count; j++) {
                    string relPath = excludes[j];
                    if (string.IsNullOrWhiteSpace(relPath)) {
                        continue;
                    }

                    string fullPath = Path.GetFullPath(Path.Combine(engineRoot, relPath.Trim()));
                    excludeSet.Add(fullPath);
                }
            }

            return excludeSet;
        }

        /// <summary>
        /// Applies excludes to the source file list.
        /// </summary>
        /// <param name="sourceFiles">Source file list to mutate.</param>
        /// <param name="excludeSet">Set of files to exclude.</param>
        void ApplyExcludes(List<string> sourceFiles, HashSet<string> excludeSet) {
            if (sourceFiles == null || excludeSet == null || excludeSet.Count == 0) {
                return;
            }

            for (int i = sourceFiles.Count - 1; i >= 0; i--) {
                string path = sourceFiles[i];
                if (excludeSet.Contains(path)) {
                    sourceFiles.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Adds patch include files to the source set.
        /// </summary>
        /// <param name="sourceSet">Source set to mutate.</param>
        /// <param name="patches">Resolved patches.</param>
        void AddIncludes(HashSet<string> sourceSet, IReadOnlyList<EnginePatchDefinition> patches) {
            if (sourceSet == null || patches == null) {
                return;
            }

            for (int i = 0; i < patches.Count; i++) {
                EnginePatchDefinition patch = patches[i];
                List<string> includes = patch.Manifest.IncludeFiles;
                if (includes == null || includes.Count == 0) {
                    continue;
                }

                for (int j = 0; j < includes.Count; j++) {
                    string relPath = includes[j];
                    if (string.IsNullOrWhiteSpace(relPath)) {
                        continue;
                    }

                    string fullPath = Path.GetFullPath(Path.Combine(patch.RootPath, relPath.Trim()));
                    sourceSet.Add(fullPath);
                }
            }
        }

        /// <summary>
        /// Builds the defines list from patch manifests.
        /// </summary>
        /// <param name="patches">Resolved patches.</param>
        /// <returns>Sorted list of defines.</returns>
        List<string> BuildDefines(IReadOnlyList<EnginePatchDefinition> patches) {
            var defineSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (patches != null) {
                for (int i = 0; i < patches.Count; i++) {
                    EnginePatchDefinition patch = patches[i];
                    List<string> defines = patch.Manifest.Defines;
                    if (defines == null || defines.Count == 0) {
                        continue;
                    }

                    for (int j = 0; j < defines.Count; j++) {
                        string define = defines[j];
                        if (string.IsNullOrWhiteSpace(define)) {
                            continue;
                        }

                        defineSet.Add(define.Trim());
                    }
                }
            }

            var result = new List<string>(defineSet);
            SortList(result);
            return result;
        }

        /// <summary>
        /// Sorts a list of strings using ordinal comparison.
        /// </summary>
        /// <param name="values">List to sort.</param>
        void SortList(List<string> values) {
            if (values == null) {
                return;
            }

            values.Sort(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether a path is under build artifact folders.
        /// </summary>
        /// <param name="path">Path to evaluate.</param>
        /// <returns>True when the path is under bin or obj.</returns>
        bool IsBuildArtifactPath(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return false;
            }

            string normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            return normalized.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
        }
    }
}
