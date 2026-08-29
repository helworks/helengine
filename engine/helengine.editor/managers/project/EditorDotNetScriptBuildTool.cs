using System.Diagnostics;
using System.Security;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Builds the generated scripting solution by invoking the local `dotnet` CLI.
    /// </summary>
    public sealed class EditorDotNetScriptBuildTool : IEditorScriptBuildToolWithWorkspaceLease {
        /// <summary>
        /// Build configuration used for editor-driven script reloads.
        /// </summary>
        const string BuildConfigurationValue = "Debug";

        /// <summary>
        /// CLI executable used for solution builds.
        /// </summary>
        const string DotNetExecutableName = "dotnet";

        /// <summary>
        /// Prefix for short-lived MSBuild property transport files used to preserve arbitrary output-root characters.
        /// </summary>
        const string OutputRootTransportFilePrefix = "helengine-output-root-";

        /// <summary>
        /// Temporary root used for MSBuild's scalar path properties before results are published to the requested root.
        /// </summary>
        const string MsBuildOutputRootDirectoryName = "helengine-msbuild-output-roots";

        /// <summary>
        /// Marker file used to prove that a staging or backup directory belongs to this publisher.
        /// </summary>
        const string PublicationMarkerFileName = ".helengine-publication-marker";

        /// <summary>
        /// Marker format identifier. The current format is intentionally strict and has no compatibility readers.
        /// </summary>
        const string PublicationMarkerFormat = "helengine-publication";

        /// <summary>
        /// Marker kind for a complete build tree that is waiting to be published.
        /// </summary>
        const string PublicationStagingKind = "staging";

        /// <summary>
        /// Marker kind for the previous destination retained during an atomic replacement.
        /// </summary>
        const string PublicationBackupKind = "backup";

        /// <summary>
        /// Builds one solution file and returns the captured process outcome.
        /// </summary>
        /// <param name="solutionPath">Absolute path to the generated solution file.</param>
        /// <returns>Build result describing success or failure.</returns>
        public EditorBuildExecutionResult Build(string solutionPath) {
            return Build(solutionPath, string.Empty);
        }

        /// <summary>
        /// Builds one solution with an optional invocation-specific generated-output root.
        /// </summary>
        /// <param name="solutionPath">Absolute path to the generated solution file.</param>
        /// <param name="executionOutputRootPath">Unique generated-output root for this invocation, or empty to use project metadata defaults.</param>
        /// <returns>Structured execution result describing the process outcome.</returns>
        public EditorBuildExecutionResult Build(string solutionPath, string executionOutputRootPath) {
            string workingDirectory = ResolveWorkingDirectory(solutionPath);
            using EditorGeneratedCodeWorkspaceLease workspaceLease = EditorGeneratedCodeWorkspaceLease.Acquire(workingDirectory);
            return Build(solutionPath, executionOutputRootPath, workspaceLease);
        }

        /// <summary>
        /// Builds one solution while retaining a caller-owned generated-workspace lease through evaluation and publication.
        /// </summary>
        /// <param name="solutionPath">Absolute path to the generated solution file.</param>
        /// <param name="executionOutputRootPath">Unique generated-output root for this invocation, or empty for fallback output.</param>
        /// <param name="workspaceLease">Lease acquired for the generated solution workspace.</param>
        /// <returns>Structured execution result describing the process outcome.</returns>
        public EditorBuildExecutionResult Build(
            string solutionPath,
            string executionOutputRootPath,
            EditorGeneratedCodeWorkspaceLease workspaceLease) {
            if (string.IsNullOrWhiteSpace(solutionPath)) {
                throw new ArgumentException("Solution path must be provided.", nameof(solutionPath));
            }
            if (executionOutputRootPath == null) {
                throw new ArgumentNullException(nameof(executionOutputRootPath));
            }
            if (workspaceLease == null) {
                throw new ArgumentNullException(nameof(workspaceLease));
            }

            string workingDirectory = ResolveWorkingDirectory(solutionPath);
            if (!workspaceLease.Covers(workingDirectory)) {
                throw new ArgumentException("Workspace lease does not cover the generated solution directory.", nameof(workspaceLease));
            }

            ProcessStartInfo startInfo = new ProcessStartInfo {
                FileName = DotNetExecutableName,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("build");
            startInfo.ArgumentList.Add(solutionPath);
            startInfo.ArgumentList.Add("--configuration");
            startInfo.ArgumentList.Add(BuildConfigurationValue);
            startInfo.ArgumentList.Add("--nologo");
            string outputRootTransportFilePath = string.Empty;
            string msbuildOutputRootPath = string.Empty;
            if (!string.IsNullOrWhiteSpace(executionOutputRootPath)) {
                msbuildOutputRootPath = ResolveMsBuildOutputRootPath(executionOutputRootPath);
                outputRootTransportFilePath = CreateOutputRootTransportFile(msbuildOutputRootPath);
                // Environment properties are imported before SDK project evaluation. Keeping this out of
                // ArgumentList is essential: filesystem paths are not a safe MSBuild -p transport.
                startInfo.Environment["HelengineExecutionOutputRootFile"] = outputRootTransportFilePath;
            } else {
                // Do not let an inherited invocation override defeat the stable fallback properties.
                startInfo.Environment.Remove("HelengineExecutionOutputRootFile");
                startInfo.Environment.Remove("HelengineExecutionOutputRoot");
            }

            try {
                using Process process = Process.Start(startInfo);
                if (process == null) {
                    throw new InvalidOperationException($"Failed to launch '{DotNetExecutableName}'.");
                }

                CaptureProcessOutput(process, out string stdout, out string stderr);

                if (process.ExitCode == 0) {
                    if (!string.IsNullOrWhiteSpace(msbuildOutputRootPath)) {
                        PublishBuildOutputs(msbuildOutputRootPath, executionOutputRootPath, workspaceLease);
                    }

                    return EditorBuildExecutionResult.Success($"Script build completed: {solutionPath}");
                }

                StringBuilder messageBuilder = new StringBuilder();
                messageBuilder.Append(DotNetExecutableName);
                messageBuilder.Append(" build failed with exit code ");
                messageBuilder.Append(process.ExitCode);
                messageBuilder.Append('.');

                string output = ChooseFailureOutput(stdout, stderr);
                if (!string.IsNullOrWhiteSpace(output)) {
                    messageBuilder.Append(' ');
                    messageBuilder.Append(output.Trim());
                }

                return EditorBuildExecutionResult.Failure(messageBuilder.ToString());
            } finally {
                if (!string.IsNullOrWhiteSpace(outputRootTransportFilePath)
                    && File.Exists(outputRootTransportFilePath)) {
                    File.Delete(outputRootTransportFilePath);
                }
                if (!string.IsNullOrWhiteSpace(msbuildOutputRootPath)
                    && Directory.Exists(msbuildOutputRootPath)) {
                    Directory.Delete(msbuildOutputRootPath, true);
                }
            }
        }

        /// <summary>
        /// Resolves a unique filesystem-safe temporary MSBuild root for one invocation.
        /// </summary>
        /// <param name="executionOutputRootPath">Requested output root, retained for the call contract.</param>
        /// <returns>Safe temporary MSBuild output root.</returns>
        static string ResolveMsBuildOutputRootPath(string executionOutputRootPath) {
            string temporaryRootPath = Path.Combine(
                Path.GetTempPath(),
                MsBuildOutputRootDirectoryName,
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRootPath);
            return temporaryRootPath;
        }

        /// <summary>
        /// Creates one temporary MSBuild property file carrying the exact invocation output root.
        /// </summary>
        /// <param name="executionOutputRootPath">Output root to transport to the child process.</param>
        /// <returns>Path to the temporary property file.</returns>
        static string CreateOutputRootTransportFile(string executionOutputRootPath) {
            string fullOutputRootPath = Path.GetFullPath(executionOutputRootPath);
            string transportFilePath = Path.Combine(
                Path.GetTempPath(),
                OutputRootTransportFilePrefix + Guid.NewGuid().ToString("N") + ".props");
            string contents = "<Project>\n"
                + "  <PropertyGroup>\n"
                + "    <HelengineExecutionOutputRoot>"
                + SecurityElement.Escape(fullOutputRootPath)
                + "</HelengineExecutionOutputRoot>\n"
                + "  </PropertyGroup>\n"
                + "</Project>\n";
            File.WriteAllText(transportFilePath, contents, Encoding.UTF8);
            return transportFilePath;
        }

        /// <summary>
        /// Publishes completed MSBuild outputs into the exact invocation root requested by the caller.
        /// </summary>
        /// <param name="msbuildOutputRootPath">Safe temporary MSBuild output root.</param>
        /// <param name="executionOutputRootPath">Exact caller-visible output root.</param>
        static void PublishBuildOutputs(
            string msbuildOutputRootPath,
            string executionOutputRootPath,
            EditorGeneratedCodeWorkspaceLease workspaceLease) {
            if (!Directory.Exists(msbuildOutputRootPath)) {
                return;
            }

            string destinationRootPath = Path.GetFullPath(executionOutputRootPath);
            if (workspaceLease.Matches(destinationRootPath)) {
                throw new InvalidOperationException("Generated compiler output cannot replace its metadata workspace.");
            }

            using EditorGeneratedCodeWorkspaceLease destinationLease = EditorGeneratedCodeWorkspaceLease.Acquire(destinationRootPath);
            PublishBuildOutputsUnderLease(msbuildOutputRootPath, destinationLease.WorkspaceRootPath);
        }

        /// <summary>
        /// Publishes one complete output tree under a destination lease by swapping a sibling staging directory.
        /// </summary>
        static void PublishBuildOutputsUnderLease(string msbuildOutputRootPath, string destinationRootPath) {
            string destinationParentPath = Path.GetDirectoryName(destinationRootPath);
            if (string.IsNullOrWhiteSpace(destinationParentPath)) {
                throw new InvalidOperationException("Generated compiler output destination must have a parent directory.");
            }

            Directory.CreateDirectory(destinationParentPath);
            string destinationName = Path.GetFileName(destinationRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(destinationName)) {
                throw new InvalidOperationException("Generated compiler output destination must have a directory name.");
            }

            RecoverPublicationLeftovers(destinationParentPath, destinationName, destinationRootPath);
            string stagingToken = Guid.NewGuid().ToString("N");
            string backupToken = Guid.NewGuid().ToString("N");
            string stagingRootPath = Path.Combine(destinationParentPath, destinationName + ".staging-" + stagingToken);
            string backupRootPath = Path.Combine(destinationParentPath, destinationName + ".backup-" + backupToken);
            bool destinationMoved = false;
            try {
                CopyDirectory(msbuildOutputRootPath, stagingRootPath);
                WritePublicationMarker(stagingRootPath, destinationRootPath, PublicationStagingKind, stagingToken);
                if (Directory.Exists(destinationRootPath)) {
                    WritePublicationMarker(destinationRootPath, destinationRootPath, PublicationBackupKind, backupToken);
                    Directory.Move(destinationRootPath, backupRootPath);
                    destinationMoved = true;
                }

                Directory.Move(stagingRootPath, destinationRootPath);
                RemovePublicationMarker(destinationRootPath, destinationRootPath, PublicationStagingKind, stagingToken);
                if (destinationMoved && Directory.Exists(backupRootPath)) {
                    Directory.Delete(backupRootPath, true);
                }
            } catch {
                if (!Directory.Exists(destinationRootPath) && destinationMoved && Directory.Exists(backupRootPath)) {
                    Directory.Move(backupRootPath, destinationRootPath);
                    RemovePublicationMarker(destinationRootPath, destinationRootPath, PublicationBackupKind, backupToken);
                }

                throw;
            } finally {
                if (Directory.Exists(stagingRootPath)) {
                    Directory.Delete(stagingRootPath, true);
                }
                if (Directory.Exists(backupRootPath)
                    && Directory.Exists(destinationRootPath)
                    && IsOwnedPublicationArtifact(backupRootPath, destinationRootPath, PublicationBackupKind, backupToken)) {
                    Directory.Delete(backupRootPath, true);
                }
            }
        }

        /// <summary>
        /// Recovers crash leftovers deterministically while the destination lease is held.
        /// </summary>
        static void RecoverPublicationLeftovers(string parentPath, string destinationName, string destinationRootPath) {
            RemoveOwnedDestinationMarker(destinationRootPath);
            string stagingPrefix = destinationName + ".staging-";
            string backupPrefix = destinationName + ".backup-";
            StringComparison pathComparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            string[] stagingPaths = Directory.EnumerateDirectories(parentPath, "*", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetFileName(path).StartsWith(stagingPrefix, pathComparison))
                .ToArray();
            Array.Sort(stagingPaths, StringComparer.Ordinal);
            string[] backupPaths = Directory.EnumerateDirectories(parentPath, "*", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetFileName(path).StartsWith(backupPrefix, pathComparison))
                .ToArray();
            Array.Sort(backupPaths, StringComparer.Ordinal);

            if (!Directory.Exists(destinationRootPath) && backupPaths.Length > 0) {
                for (int index = 0; index < backupPaths.Length; index++) {
                    if (!IsOwnedPublicationArtifact(backupPaths[index], destinationRootPath, PublicationBackupKind)) {
                        continue;
                    }

                    Directory.Move(backupPaths[index], destinationRootPath);
                    RemoveOwnedDestinationMarker(destinationRootPath);
                    backupPaths[index] = string.Empty;
                    break;
                }
            }

            for (int index = 0; index < stagingPaths.Length; index++) {
                if (Directory.Exists(stagingPaths[index])
                    && IsOwnedPublicationArtifact(stagingPaths[index], destinationRootPath, PublicationStagingKind)) {
                    Directory.Delete(stagingPaths[index], true);
                }
            }
            for (int index = 0; index < backupPaths.Length; index++) {
                if (!string.IsNullOrEmpty(backupPaths[index])
                    && Directory.Exists(backupPaths[index])
                    && IsOwnedPublicationArtifact(backupPaths[index], destinationRootPath, PublicationBackupKind)) {
                    Directory.Delete(backupPaths[index], true);
                }
            }
        }

        /// <summary>
        /// Writes one atomically published ownership marker into a candidate artifact directory.
        /// </summary>
        static void WritePublicationMarker(string artifactRootPath, string destinationRootPath, string kind, string token) {
            if (!IsIssuedPublicationToken(token)) {
                throw new ArgumentException("Publication token must be a lowercase GUID without separators.", nameof(token));
            }
            if (kind != PublicationStagingKind && kind != PublicationBackupKind) {
                throw new ArgumentException("Unknown publication artifact kind.", nameof(kind));
            }

            string markerPath = Path.Combine(artifactRootPath, PublicationMarkerFileName);
            string temporaryMarkerPath = markerPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            string markerContents = PublicationMarkerFormat + "\n"
                + NormalizeDestinationIdentity(destinationRootPath) + "\n"
                + kind + "\n"
                + token + "\n";
            try {
                File.WriteAllText(temporaryMarkerPath, markerContents, Encoding.UTF8);
                File.Move(temporaryMarkerPath, markerPath, false);
            } finally {
                if (File.Exists(temporaryMarkerPath)) {
                    File.Delete(temporaryMarkerPath);
                }
            }
        }

        /// <summary>
        /// Removes one marker only when it still proves the expected current artifact ownership.
        /// </summary>
        static void RemovePublicationMarker(string artifactRootPath, string destinationRootPath, string kind, string token) {
            string markerPath = Path.Combine(artifactRootPath, PublicationMarkerFileName);
            if (File.Exists(markerPath) && IsOwnedPublicationArtifact(artifactRootPath, destinationRootPath, kind, token)) {
                File.Delete(markerPath);
            }
        }

        /// <summary>
        /// Removes an owned marker left in the final destination after an interrupted rename.
        /// </summary>
        static void RemoveOwnedDestinationMarker(string destinationRootPath) {
            string markerPath = Path.Combine(destinationRootPath, PublicationMarkerFileName);
            if (!File.Exists(markerPath)) {
                return;
            }

            if (IsOwnedPublicationArtifact(destinationRootPath, destinationRootPath, PublicationStagingKind)
                || IsOwnedPublicationArtifact(destinationRootPath, destinationRootPath, PublicationBackupKind)) {
                File.Delete(markerPath);
            }
        }

        /// <summary>
        /// Validates the complete ownership marker, including destination, phase, and issued token.
        /// </summary>
        static bool IsOwnedPublicationArtifact(
            string artifactRootPath,
            string destinationRootPath,
            string expectedKind,
            string expectedToken = null) {
            string markerPath = Path.Combine(artifactRootPath, PublicationMarkerFileName);
            if (!File.Exists(markerPath)) {
                return false;
            }

            string[] markerLines;
            try {
                markerLines = File.ReadAllLines(markerPath, Encoding.UTF8);
            } catch (IOException) {
                return false;
            } catch (UnauthorizedAccessException) {
                return false;
            }

            if (markerLines.Length != 4
                || markerLines[0] != PublicationMarkerFormat
                || markerLines[1] != NormalizeDestinationIdentity(destinationRootPath)
                || markerLines[2] != expectedKind
                || !IsIssuedPublicationToken(markerLines[3])) {
                return false;
            }

            bool isDestinationArtifact = NormalizeDestinationIdentity(artifactRootPath)
                == NormalizeDestinationIdentity(destinationRootPath);
            if (!isDestinationArtifact) {
                string expectedDirectoryPrefix = expectedKind == PublicationStagingKind
                    ? ".staging-"
                    : ".backup-";
                string artifactName = Path.GetFileName(artifactRootPath);
                string destinationName = Path.GetFileName(destinationRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                StringComparison nameComparison = OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
                if (artifactName == null
                    || !artifactName.Equals(destinationName + expectedDirectoryPrefix + markerLines[3], nameComparison)) {
                    return false;
                }
            }

            return expectedToken == null || markerLines[3] == expectedToken;
        }

        /// <summary>
        /// Validates the deliberately narrow token grammar used by publication markers.
        /// </summary>
        static bool IsIssuedPublicationToken(string value) {
            return value != null
                && value.Length == 32
                && Guid.TryParseExact(value, "N", out Guid parsedToken)
                && parsedToken.ToString("N") == value;
        }

        /// <summary>
        /// Resolves the canonical identity serialized into a publication marker.
        /// </summary>
        static string NormalizeDestinationIdentity(string destinationRootPath) {
            string fullPath = Path.GetFullPath(destinationRootPath);
            string rootPath = Path.GetPathRoot(fullPath) ?? string.Empty;
            string trimmedPath = fullPath.Length > rootPath.Length
                ? fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : fullPath;
            return OperatingSystem.IsWindows() ? trimmedPath.ToUpperInvariant() : trimmedPath;
        }

        /// <summary>
        /// Copies a complete build tree to a sibling staging directory.
        /// </summary>
        static void CopyDirectory(string sourceRootPath, string destinationRootPath) {
            Directory.CreateDirectory(destinationRootPath);
            foreach (string sourceDirectoryPath in Directory.GetDirectories(sourceRootPath, "*", SearchOption.AllDirectories)) {
                string relativeDirectoryPath = Path.GetRelativePath(sourceRootPath, sourceDirectoryPath);
                Directory.CreateDirectory(Path.Combine(destinationRootPath, relativeDirectoryPath));
            }
            foreach (string sourceFilePath in Directory.GetFiles(sourceRootPath, "*", SearchOption.AllDirectories)) {
                string relativeFilePath = Path.GetRelativePath(sourceRootPath, sourceFilePath);
                string destinationFilePath = Path.Combine(destinationRootPath, relativeFilePath);
                string destinationDirectoryPath = Path.GetDirectoryName(destinationFilePath);
                if (!string.IsNullOrWhiteSpace(destinationDirectoryPath)) {
                    Directory.CreateDirectory(destinationDirectoryPath);
                }
                File.Copy(sourceFilePath, destinationFilePath, false);
            }
        }

        /// <summary>
        /// Resolves and validates the generated-solution working directory.
        /// </summary>
        static string ResolveWorkingDirectory(string solutionPath) {
            if (string.IsNullOrWhiteSpace(solutionPath)) {
                throw new ArgumentException("Solution path must be provided.", nameof(solutionPath));
            }

            string? workingDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionPath));
            return string.IsNullOrWhiteSpace(workingDirectory)
                ? Environment.CurrentDirectory
                : workingDirectory;
        }

        /// <summary>
        /// Chooses the most useful captured output for a failed `dotnet` build.
        /// </summary>
        /// <param name="stdout">Captured standard output.</param>
        /// <param name="stderr">Captured standard error.</param>
        /// <returns>Preferred failure output text.</returns>
        string ChooseFailureOutput(string stdout, string stderr) {
            if (!string.IsNullOrWhiteSpace(stderr)) {
                return stderr;
            }

            return stdout ?? string.Empty;
        }

        /// <summary>
        /// Captures the redirected process output streams without risking the deadlock caused by sequential synchronous pipe reads on warning-heavy builds.
        /// </summary>
        /// <param name="process">Running build process whose redirected streams should be drained.</param>
        /// <param name="stdout">Captured standard output text.</param>
        /// <param name="stderr">Captured standard error text.</param>
        void CaptureProcessOutput(Process process, out string stdout, out string stderr) {
            if (process == null) {
                throw new ArgumentNullException(nameof(process));
            }

            StringBuilder stdoutBuilder = new StringBuilder();
            StringBuilder stderrBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, eventArgs) => AppendCapturedLine(stdoutBuilder, eventArgs.Data);
            process.ErrorDataReceived += (sender, eventArgs) => AppendCapturedLine(stderrBuilder, eventArgs.Data);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            stdout = stdoutBuilder.ToString();
            stderr = stderrBuilder.ToString();
        }

        /// <summary>
        /// Appends one captured process-output line to the supplied buffer while preserving line boundaries.
        /// </summary>
        /// <param name="builder">Destination text buffer.</param>
        /// <param name="line">Captured output line.</param>
        void AppendCapturedLine(StringBuilder builder, string line) {
            if (builder == null) {
                throw new ArgumentNullException(nameof(builder));
            } else if (line == null) {
                return;
            }

            builder.AppendLine(line);
        }
    }
}
