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
        /// Sibling directory containing the operation manifest for one publication token.
        /// </summary>
        const string PublicationOperationDirectorySuffix = ".operation-";

        /// <summary>
        /// Marker file inside one operation directory.
        /// </summary>
        const string PublicationOperationMarkerFileName = ".helengine-publication-operation";

        /// <summary>
        /// Current publication marker format. There is no compatibility reader for older formats.
        /// </summary>
        const string PublicationOperationFormat = "helengine-publication";

        /// <summary>
        /// Operation phase before a complete staging tree exists.
        /// </summary>
        const string PublicationPreparedPhase = "prepared";

        /// <summary>
        /// Operation phase after a complete staging tree exists.
        /// </summary>
        const string PublicationStagedPhase = "staged";

        /// <summary>
        /// Operation phase immediately before moving the live destination to its backup sibling.
        /// </summary>
        const string PublicationBackupMovingPhase = "backup-moving";

        /// <summary>
        /// Operation phase after the live destination has moved to the backup sibling.
        /// </summary>
        const string PublicationBackupMovedPhase = "backup-moved";

        /// <summary>
        /// Operation phase after the complete staged tree has become the destination.
        /// </summary>
        const string PublicationDestinationMovedPhase = "destination-moved";

        /// <summary>
        /// Optional deterministic move-failure seam used by publication recovery tests.
        /// </summary>
        internal static Action<string, string> PublicationMoveHookForTests { get; set; }

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
            string stagingRootPath = Path.Combine(destinationParentPath, destinationName + ".staging-" + stagingToken);
            string backupRootPath = Path.Combine(destinationParentPath, destinationName + ".backup-" + stagingToken);
            string operationRootPath = Path.Combine(destinationParentPath, destinationName + PublicationOperationDirectorySuffix + stagingToken);
            bool destinationMoved = false;
            bool retainOwnedArtifactsForRecovery = false;
            try {
                Directory.CreateDirectory(operationRootPath);
                WritePublicationOperationMarker(operationRootPath, destinationRootPath, stagingToken, PublicationPreparedPhase);
                CopyDirectory(msbuildOutputRootPath, stagingRootPath);
                WritePublicationOperationMarker(operationRootPath, destinationRootPath, stagingToken, PublicationStagedPhase);
                if (Directory.Exists(destinationRootPath)) {
                    WritePublicationOperationMarker(operationRootPath, destinationRootPath, stagingToken, PublicationBackupMovingPhase);
                    InvokePublicationMoveHook(destinationRootPath, backupRootPath);
                    Directory.Move(destinationRootPath, backupRootPath);
                    destinationMoved = true;
                    WritePublicationOperationMarker(operationRootPath, destinationRootPath, stagingToken, PublicationBackupMovedPhase);
                }

                InvokePublicationMoveHook(stagingRootPath, destinationRootPath);
                Directory.Move(stagingRootPath, destinationRootPath);
                WritePublicationOperationMarker(operationRootPath, destinationRootPath, stagingToken, PublicationDestinationMovedPhase);
                if (destinationMoved && Directory.Exists(backupRootPath)) {
                    Directory.Delete(backupRootPath, true);
                }
                Directory.Delete(operationRootPath, true);
            } catch {
                if (!Directory.Exists(destinationRootPath) && destinationMoved && Directory.Exists(backupRootPath)) {
                    try {
                        InvokePublicationMoveHook(backupRootPath, destinationRootPath);
                        Directory.Move(backupRootPath, destinationRootPath);
                    } catch {
                        // Keep the operation manifest and exact owned siblings for the next recovery pass.
                        retainOwnedArtifactsForRecovery = true;
                    }
                } else if (!Directory.Exists(destinationRootPath) && destinationMoved) {
                    // The previous destination was moved, but its owned backup is no longer available.
                    // Preserve the operation record rather than guessing at foreign siblings.
                    retainOwnedArtifactsForRecovery = true;
                }

                if (!retainOwnedArtifactsForRecovery) {
                    DeleteDirectoryIfPresent(stagingRootPath);
                    DeleteDirectoryIfPresent(backupRootPath);
                    DeleteDirectoryIfPresent(operationRootPath);
                }

                throw;
            } finally {
                if (!retainOwnedArtifactsForRecovery) {
                    DeleteDirectoryIfPresent(stagingRootPath);
                }
            }
        }

        /// <summary>
        /// Recovers crash leftovers deterministically while the destination lease is held.
        /// </summary>
        static void RecoverPublicationLeftovers(string parentPath, string destinationName, string destinationRootPath) {
            string operationPrefix = destinationName + PublicationOperationDirectorySuffix;
            StringComparison pathComparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            string[] operationPaths = Directory.EnumerateDirectories(parentPath, "*", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetFileName(path).StartsWith(operationPrefix, pathComparison))
                .ToArray();
            Array.Sort(operationPaths, StringComparer.Ordinal);

            for (int index = 0; index < operationPaths.Length; index++) {
                if (IsOwnedPublicationOperation(operationPaths[index], destinationRootPath, out string token, out string phase)) {
                    RecoverPublicationOperation(operationPaths[index], destinationRootPath, token, phase);
                }
            }
        }

        /// <summary>
        /// Recovers one positively identified publication operation and its exact token-bound siblings.
        /// </summary>
        static void RecoverPublicationOperation(string operationRootPath, string destinationRootPath, string token, string phase) {
            string destinationParentPath = Path.GetDirectoryName(destinationRootPath);
            string destinationName = Path.GetFileName(destinationRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(destinationParentPath) || string.IsNullOrWhiteSpace(destinationName)) {
                return;
            }

            string stagingRootPath = Path.Combine(destinationParentPath, destinationName + ".staging-" + token);
            string backupRootPath = Path.Combine(destinationParentPath, destinationName + ".backup-" + token);
            if (!Directory.Exists(destinationRootPath)) {
                if (Directory.Exists(stagingRootPath) && phase != PublicationPreparedPhase) {
                    Directory.Move(stagingRootPath, destinationRootPath);
                } else if (Directory.Exists(backupRootPath)) {
                    Directory.Move(backupRootPath, destinationRootPath);
                }
            }

            if (Directory.Exists(destinationRootPath)) {
                DeleteDirectoryIfPresent(stagingRootPath);
                DeleteDirectoryIfPresent(backupRootPath);
                DeleteDirectoryIfPresent(operationRootPath);
            }
        }

        /// <summary>
        /// Writes one atomically published operation marker with the exact destination and phase.
        /// </summary>
        static void WritePublicationOperationMarker(string operationRootPath, string destinationRootPath, string token, string phase) {
            if (!IsIssuedPublicationToken(token)) {
                throw new ArgumentException("Publication token must be a lowercase GUID without separators.", nameof(token));
            }
            if (!IsPublicationPhase(phase)) {
                throw new ArgumentException("Unknown publication operation phase.", nameof(phase));
            }

            string markerPath = Path.Combine(operationRootPath, PublicationOperationMarkerFileName);
            string temporaryMarkerPath = markerPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            string markerContents = PublicationOperationFormat + "\n"
                + NormalizeDestinationIdentity(destinationRootPath) + "\n"
                + token + "\n"
                + phase + "\n";
            try {
                File.WriteAllText(temporaryMarkerPath, markerContents, Encoding.UTF8);
                File.Move(temporaryMarkerPath, markerPath, true);
            } finally {
                if (File.Exists(temporaryMarkerPath)) {
                    File.Delete(temporaryMarkerPath);
                }
            }
        }

        /// <summary>
        /// Determines whether one sibling operation directory is positively owned by this publisher.
        /// </summary>
        static bool IsOwnedPublicationOperation(string operationRootPath, string destinationRootPath, out string token, out string phase) {
            token = string.Empty;
            phase = string.Empty;
            string destinationName = Path.GetFileName(destinationRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string operationName = Path.GetFileName(operationRootPath);
            string operationPrefix = destinationName + PublicationOperationDirectorySuffix;
            if (string.IsNullOrWhiteSpace(destinationName)
                || operationName == null
                || !operationName.StartsWith(operationPrefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) {
                return false;
            }
            token = operationName[operationPrefix.Length..];
            if (!IsIssuedPublicationToken(token)) {
                return false;
            }

            string markerPath = Path.Combine(operationRootPath, PublicationOperationMarkerFileName);
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
                || markerLines[0] != PublicationOperationFormat
                || markerLines[1] != NormalizeDestinationIdentity(destinationRootPath)
                || markerLines[2] != token
                || !IsIssuedPublicationToken(markerLines[2])
                || !IsPublicationPhase(markerLines[3])) {
                return false;
            }

            phase = markerLines[3];
            return true;
        }

        /// <summary>
        /// Determines whether one marker phase is a current publication phase.
        /// </summary>
        static bool IsPublicationPhase(string phase) {
            return phase == PublicationPreparedPhase
                || phase == PublicationStagedPhase
                || phase == PublicationBackupMovingPhase
                || phase == PublicationBackupMovedPhase
                || phase == PublicationDestinationMovedPhase;
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
        /// Invokes the deterministic move-failure seam when a publication test has installed one.
        /// </summary>
        static void InvokePublicationMoveHook(string sourcePath, string destinationPath) {
            PublicationMoveHookForTests?.Invoke(sourcePath, destinationPath);
        }

        /// <summary>
        /// Removes one directory tree when it exists.
        /// </summary>
        static void DeleteDirectoryIfPresent(string directoryPath) {
            if (Directory.Exists(directoryPath)) {
                Directory.Delete(directoryPath, true);
            }
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
