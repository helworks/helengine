using System.Diagnostics;
using System.Security;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Builds the generated scripting solution by invoking the local `dotnet` CLI.
    /// </summary>
    public sealed class EditorDotNetScriptBuildTool : IEditorScriptBuildToolWithWorkspaceLease {
        /// <summary>
        /// Build configuration every generated script build runs with. <see cref="EditorGeneratedCodeSolutionBuilder"/>
        /// uses the same value to report the SDK default output directory, so the two cannot disagree.
        /// </summary>
        internal const string BuildConfigurationValue = "Debug";

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
        /// File name of the stable invocation property file an incremental build keeps inside its output root.
        /// </summary>
        const string IncrementalPropertyFileName = "helengine-execution-output.props";

        /// <summary>
        /// File name of the stamp recording which restore inputs the last successful restoring incremental build saw.
        /// </summary>
        public const string IncrementalRestoreStampFileName = "helengine-restore.stamp";

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
        /// Failure message of a build its caller cancelled; the cancelled build's output is incomplete.
        /// </summary>
        public const string CancelledMessage = "The script build was cancelled.";

        /// <summary>
        /// Optional deterministic move-failure seam used by publication recovery tests.
        /// </summary>
        internal static Action<string, string> PublicationMoveHookForTests { get; set; }

        /// <summary>
        /// Session job the cancellable builds' processes join so their MSBuild nodes and compiler server stay warm until
        /// the editor closes, or null for a tool whose builds are not cancellable.
        /// </summary>
        readonly EditorSessionProcessJob SessionJob;

        /// <summary>
        /// Initializes a build tool whose builds run without a session job.
        /// </summary>
        public EditorDotNetScriptBuildTool() {
        }

        /// <summary>
        /// Initializes a build tool whose cancellable builds join the given session job.
        /// </summary>
        /// <param name="sessionJob">Session job that keeps build nodes warm until the editor closes.</param>
        /// <exception cref="ArgumentNullException">Thrown when the session job is null.</exception>
        public EditorDotNetScriptBuildTool(EditorSessionProcessJob sessionJob) {
            SessionJob = sessionJob ?? throw new ArgumentNullException(nameof(sessionJob));
        }

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
        /// Builds one solution into an invocation-specific output root while defining extra preprocessor symbols for
        /// every generated module project, such as a platform's gameplay symbols for an editor Play build.
        /// </summary>
        /// <param name="solutionPath">Absolute path to the generated solution file.</param>
        /// <param name="executionOutputRootPath">Unique generated-output root for this invocation, or empty to use project metadata defaults.</param>
        /// <param name="additionalDefineConstants">Symbols appended to the projects' define constants; an empty list builds exactly like <see cref="Build(string, string)"/>.</param>
        /// <returns>Structured execution result describing the process outcome.</returns>
        public EditorBuildExecutionResult Build(
            string solutionPath,
            string executionOutputRootPath,
            IReadOnlyList<string> additionalDefineConstants) {
            if (additionalDefineConstants == null) {
                throw new ArgumentNullException(nameof(additionalDefineConstants));
            }

            string workingDirectory = ResolveWorkingDirectory(solutionPath);
            using EditorGeneratedCodeWorkspaceLease workspaceLease = EditorGeneratedCodeWorkspaceLease.Acquire(workingDirectory);
            return Build(solutionPath, executionOutputRootPath, workspaceLease, additionalDefineConstants);
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
            return Build(solutionPath, executionOutputRootPath, workspaceLease, Array.Empty<string>());
        }

        /// <summary>
        /// Builds one solution under a caller-owned generated-workspace lease while defining extra preprocessor symbols
        /// for every generated module project.
        /// </summary>
        /// <param name="solutionPath">Absolute path to the generated solution file.</param>
        /// <param name="executionOutputRootPath">Unique generated-output root for this invocation, or empty for fallback output.</param>
        /// <param name="workspaceLease">Lease acquired for the generated solution workspace.</param>
        /// <param name="additionalDefineConstants">Symbols appended to the projects' define constants. They travel in the
        /// invocation property file, so a non-empty list requires an execution output root.</param>
        /// <returns>Structured execution result describing the process outcome.</returns>
        public EditorBuildExecutionResult Build(
            string solutionPath,
            string executionOutputRootPath,
            EditorGeneratedCodeWorkspaceLease workspaceLease,
            IReadOnlyList<string> additionalDefineConstants) {
            if (string.IsNullOrWhiteSpace(solutionPath)) {
                throw new ArgumentException("Solution path must be provided.", nameof(solutionPath));
            }
            if (executionOutputRootPath == null) {
                throw new ArgumentNullException(nameof(executionOutputRootPath));
            }
            if (workspaceLease == null) {
                throw new ArgumentNullException(nameof(workspaceLease));
            }
            if (additionalDefineConstants == null) {
                throw new ArgumentNullException(nameof(additionalDefineConstants));
            }
            if (additionalDefineConstants.Count > 0 && string.IsNullOrWhiteSpace(executionOutputRootPath)) {
                throw new ArgumentException(
                    "Additional define constants travel in the invocation property file and require an execution output root.",
                    nameof(additionalDefineConstants));
            }

            string workingDirectory = ResolveWorkingDirectory(solutionPath);
            if (!workspaceLease.Covers(workingDirectory)) {
                throw new ArgumentException("Workspace lease does not cover the generated solution directory.", nameof(workspaceLease));
            }

            ProcessStartInfo startInfo = CreateBuildStartInfo(solutionPath, workingDirectory);
            string outputRootTransportFilePath = string.Empty;
            string msbuildOutputRootPath = string.Empty;
            if (!string.IsNullOrWhiteSpace(executionOutputRootPath)) {
                msbuildOutputRootPath = ResolveMsBuildOutputRootPath(executionOutputRootPath);
                outputRootTransportFilePath = CreateOutputRootTransportFile(msbuildOutputRootPath, additionalDefineConstants);
                // Environment properties are imported before SDK project evaluation. Keeping this out of
                // ArgumentList is essential: filesystem paths are not a safe MSBuild -p transport.
                startInfo.Environment["HelengineExecutionOutputRootFile"] = outputRootTransportFilePath;
            } else {
                // Do not let an inherited invocation override defeat the stable fallback properties.
                startInfo.Environment.Remove("HelengineExecutionOutputRootFile");
                startInfo.Environment.Remove("HelengineExecutionOutputRoot");
            }

            try {
                EditorBuildExecutionResult result = RunBuildProcess(startInfo, solutionPath, null);
                if (result.Succeeded && !string.IsNullOrWhiteSpace(msbuildOutputRootPath)) {
                    PublishBuildOutputs(msbuildOutputRootPath, executionOutputRootPath, workspaceLease);
                }

                return result;
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
        /// Builds one solution incrementally, with MSBuild writing its intermediates and assemblies directly beneath a
        /// stable caller-owned output root instead of a fresh temporary root that is then published. Because
        /// <c>obj</c> and <c>bin</c> survive between builds, MSBuild skips every project whose inputs did not change, and
        /// restore is skipped entirely (<c>--no-restore</c>) while the restore inputs are unchanged since the last
        /// successful restore and every project's restore output still exists. The invocation property file also lives at a stable path inside the output root and is
        /// rewritten only when its contents change: MSBuild treats imported files as compile inputs, so a fresh file
        /// would force every project to recompile. Unlike <see cref="Build(string, string, EditorGeneratedCodeWorkspaceLease, IReadOnlyList{string})"/>
        /// a failed build can leave some projects updated, so callers must not treat the output as complete until it succeeds.
        /// </summary>
        /// <param name="solutionPath">Absolute path to the generated solution file.</param>
        /// <param name="outputRootPath">Stable output root that receives <c>generated_code/obj</c> and <c>generated_code/bin</c>.</param>
        /// <param name="workspaceLease">Lease acquired for the generated solution workspace.</param>
        /// <param name="additionalDefineConstants">Symbols appended to every generated project's define constants.</param>
        /// <param name="projectFilePaths">Generated project files the solution builds; each needs restore output before restore can be skipped.</param>
        /// <param name="restoreInputFilePaths">Generated files whose contents decide what restore produces, such as the
        /// solution, project and build property files. Restore is skipped only while their contents, and the invocation
        /// property file, are byte-identical to the last build that restored successfully.</param>
        /// <param name="cancellation">Cancels the build: a cancelled build never starts <c>dotnet</c>, and cancelling a
        /// running one kills its whole process tree. The result is then a failure carrying <see cref="CancelledMessage"/>.</param>
        /// <returns>Structured execution result describing the process outcome.</returns>
        public EditorBuildExecutionResult BuildIncrementally(
            string solutionPath,
            string outputRootPath,
            EditorGeneratedCodeWorkspaceLease workspaceLease,
            IReadOnlyList<string> additionalDefineConstants,
            IReadOnlyList<string> projectFilePaths,
            IReadOnlyList<string> restoreInputFilePaths,
            EditorProcessCancellation cancellation) {
            if (string.IsNullOrWhiteSpace(solutionPath)) {
                throw new ArgumentException("Solution path must be provided.", nameof(solutionPath));
            } else if (string.IsNullOrWhiteSpace(outputRootPath)) {
                throw new ArgumentException("Output root path must be provided.", nameof(outputRootPath));
            } else if (workspaceLease == null) {
                throw new ArgumentNullException(nameof(workspaceLease));
            } else if (additionalDefineConstants == null) {
                throw new ArgumentNullException(nameof(additionalDefineConstants));
            } else if (projectFilePaths == null) {
                throw new ArgumentNullException(nameof(projectFilePaths));
            } else if (restoreInputFilePaths == null) {
                throw new ArgumentNullException(nameof(restoreInputFilePaths));
            } else if (cancellation == null) {
                throw new ArgumentNullException(nameof(cancellation));
            }

            string workingDirectory = ResolveWorkingDirectory(solutionPath);
            if (!workspaceLease.Covers(workingDirectory)) {
                throw new ArgumentException("Workspace lease does not cover the generated solution directory.", nameof(workspaceLease));
            }

            string fullOutputRootPath = Path.GetFullPath(outputRootPath);
            if (workspaceLease.Matches(fullOutputRootPath)) {
                throw new InvalidOperationException("Generated compiler output cannot replace its metadata workspace.");
            }

            Directory.CreateDirectory(fullOutputRootPath);
            using EditorGeneratedCodeWorkspaceLease outputLease = EditorGeneratedCodeWorkspaceLease.Acquire(fullOutputRootPath);
            string propertyFilePath = Path.Combine(outputLease.WorkspaceRootPath, IncrementalPropertyFileName);
            EditorGameSolutionService.WriteTextIfChanged(
                propertyFilePath,
                BuildExecutionOutputPropsXml(outputLease.WorkspaceRootPath, additionalDefineConstants));
            List<string> restoreInputs = new List<string>(restoreInputFilePaths.Count + 1);
            restoreInputs.AddRange(restoreInputFilePaths);
            restoreInputs.Add(propertyFilePath);
            string restoreStamp = EditorScriptBuildFingerprint.Compute(new EditorScriptBuildInputs(
                restoreInputs,
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { "restore-stamp=1" }));
            string restoreStampFilePath = Path.Combine(outputLease.WorkspaceRootPath, IncrementalRestoreStampFileName);
            bool restoreCurrent = EditorScriptBuildFingerprint.MatchesStored(restoreStampFilePath, restoreStamp)
                && AllRestoreOutputsExist(outputLease.WorkspaceRootPath, projectFilePaths);

            ProcessStartInfo startInfo = CreateBuildStartInfo(solutionPath, workingDirectory);
            startInfo.Environment["HelengineExecutionOutputRootFile"] = propertyFilePath;
            if (restoreCurrent) {
                startInfo.ArgumentList.Add("--no-restore");
            } else if (File.Exists(restoreStampFilePath)) {
                // This build restores for new inputs and rewrites the restore output even if the compile later fails, so
                // the old stamp must go first: reverting to the old inputs must restore again, not reuse these outputs.
                File.Delete(restoreStampFilePath);
            }

            if (cancellation.IsCancellationRequested) {
                return EditorBuildExecutionResult.Failure(CancelledMessage);
            }

            EditorBuildExecutionResult result = RunBuildProcess(startInfo, solutionPath, cancellation);
            if (result.Succeeded && !restoreCurrent) {
                EditorScriptBuildFingerprint.WriteStored(restoreStampFilePath, restoreStamp);
            } else if (!result.Succeeded && restoreCurrent && File.Exists(restoreStampFilePath)) {
                // A build that skipped restore failed; restore again next time rather than trusting the restore output.
                File.Delete(restoreStampFilePath);
            }

            return result;
        }

        /// <summary>
        /// Checks that every generated project still has the restore output an incremental build skips restore for.
        /// The generated build properties place each project's intermediates under
        /// <c>&lt;output root&gt;/generated_code/obj/&lt;project name&gt;</c>.
        /// </summary>
        /// <param name="outputRootPath">Incremental output root holding <c>generated_code/obj</c>.</param>
        /// <param name="projectFilePaths">Generated project files the solution builds.</param>
        /// <returns>True when every project's assets and NuGet property imports exist.</returns>
        static bool AllRestoreOutputsExist(string outputRootPath, IReadOnlyList<string> projectFilePaths) {
            for (int index = 0; index < projectFilePaths.Count; index++) {
                string projectName = Path.GetFileNameWithoutExtension(projectFilePaths[index]);
                string intermediateDirectoryPath = Path.Combine(outputRootPath, EditorGeneratedCodePaths.GeneratedCodeFolderName, "obj", projectName);
                if (!File.Exists(Path.Combine(intermediateDirectoryPath, "project.assets.json"))
                    || !File.Exists(Path.Combine(intermediateDirectoryPath, Path.GetFileName(projectFilePaths[index]) + ".nuget.g.props"))) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Creates the <c>dotnet build</c> process description shared by every build mode.
        /// </summary>
        /// <param name="solutionPath">Absolute path to the generated solution file.</param>
        /// <param name="workingDirectory">Directory the build runs in.</param>
        /// <returns>Process start description with redirected output and the build arguments.</returns>
        static ProcessStartInfo CreateBuildStartInfo(string solutionPath, string workingDirectory) {
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
            // Generated module descriptors own their external bin/obj paths through the
            // invocation props. A host-wide artifacts layout must not redirect them.
            startInfo.ArgumentList.Add("-p:UseArtifactsOutput=false");
            startInfo.ArgumentList.Add("--nologo");
            return startInfo;
        }

        /// <summary>
        /// Runs one <c>dotnet build</c> process to completion and converts its exit code and output into a result.
        /// </summary>
        /// <param name="startInfo">Fully configured build process description.</param>
        /// <param name="solutionPath">Solution being built, named in the success message.</param>
        /// <param name="cancellation">Cancels the build by killing its process tree, or null for a build that is not cancellable.</param>
        /// <returns>Success when the process exits with code zero, a cancellation failure, or a failure carrying the build output.</returns>
        EditorBuildExecutionResult RunBuildProcess(ProcessStartInfo startInfo, string solutionPath, EditorProcessCancellation cancellation) {
            using Process process = Process.Start(startInfo);
            if (process == null) {
                throw new InvalidOperationException($"Failed to launch '{DotNetExecutableName}'.");
            }

            if (cancellation != null && !cancellation.TryAttach(process, SessionJob)) {
                process.WaitForExit();
                return EditorBuildExecutionResult.Failure(CancelledMessage);
            }

            string stdout;
            string stderr;
            try {
                // Returns when dotnet exits, not when its output pipes close: warm MSBuild nodes keep those open.
                EditorBuildProcessOutputCapture.Capture(process, out stdout, out stderr);
            } finally {
                cancellation?.Detach();
            }

            if (cancellation != null && cancellation.IsCancellationRequested) {
                return EditorBuildExecutionResult.Failure(CancelledMessage);
            } else if (process.ExitCode == 0) {
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
        /// Creates one temporary MSBuild property file carrying the exact invocation output root and any extra define constants.
        /// </summary>
        /// <param name="executionOutputRootPath">Output root to transport to the child process.</param>
        /// <param name="defineConstants">Extra preprocessor symbols to append to every generated project's define constants.</param>
        /// <returns>Path to the temporary property file.</returns>
        static string CreateOutputRootTransportFile(string executionOutputRootPath, IReadOnlyList<string> defineConstants) {
            string fullOutputRootPath = Path.GetFullPath(executionOutputRootPath);
            string transportFilePath = Path.Combine(
                Path.GetTempPath(),
                OutputRootTransportFilePrefix + Guid.NewGuid().ToString("N") + ".props");
            File.WriteAllText(transportFilePath, BuildExecutionOutputPropsXml(fullOutputRootPath, defineConstants), Encoding.UTF8);
            return transportFilePath;
        }

        /// <summary>
        /// Builds the MSBuild property file imported early by generated module projects. It always carries the
        /// invocation output root; when symbols are supplied it also appends them to the inherited define constants,
        /// which is how Play builds compile authored scripts with a platform's gameplay symbols.
        /// </summary>
        /// <param name="outputRootPath">Output root written verbatim into <c>HelengineExecutionOutputRoot</c>.</param>
        /// <param name="defineConstants">Symbols to append; an empty list emits no <c>DefineConstants</c> element.</param>
        /// <returns>Complete MSBuild project XML text.</returns>
        internal static string BuildExecutionOutputPropsXml(string outputRootPath, IReadOnlyList<string> defineConstants) {
            if (string.IsNullOrWhiteSpace(outputRootPath)) {
                throw new ArgumentException("Output root path must be provided.", nameof(outputRootPath));
            } else if (defineConstants == null) {
                throw new ArgumentNullException(nameof(defineConstants));
            }

            StringBuilder builder = new StringBuilder();
            builder.Append("<Project>\n");
            builder.Append("  <PropertyGroup>\n");
            builder.Append("    <HelengineExecutionOutputRoot>");
            builder.Append(SecurityElement.Escape(outputRootPath));
            builder.Append("</HelengineExecutionOutputRoot>\n");
            if (defineConstants.Count > 0) {
                builder.Append("    <DefineConstants>$(DefineConstants)");
                for (int index = 0; index < defineConstants.Count; index++) {
                    string symbol = defineConstants[index];
                    if (string.IsNullOrWhiteSpace(symbol) || symbol.Contains(';')) {
                        throw new ArgumentException("Define constants must be non-empty single symbols.", nameof(defineConstants));
                    }

                    builder.Append(';');
                    builder.Append(SecurityElement.Escape(symbol));
                }
                builder.Append("</DefineConstants>\n");
            }
            builder.Append("  </PropertyGroup>\n");
            builder.Append("</Project>\n");
            return builder.ToString();
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
                    EditorDirectoryPublicationMove.Move(destinationRootPath, backupRootPath);
                    destinationMoved = true;
                    WritePublicationOperationMarker(operationRootPath, destinationRootPath, stagingToken, PublicationBackupMovedPhase);
                }

                InvokePublicationMoveHook(stagingRootPath, destinationRootPath);
                EditorDirectoryPublicationMove.Move(stagingRootPath, destinationRootPath);
                WritePublicationOperationMarker(operationRootPath, destinationRootPath, stagingToken, PublicationDestinationMovedPhase);
                if (destinationMoved && Directory.Exists(backupRootPath)) {
                    Directory.Delete(backupRootPath, true);
                }
                Directory.Delete(operationRootPath, true);
            } catch {
                if (!Directory.Exists(destinationRootPath) && destinationMoved && Directory.Exists(backupRootPath)) {
                    try {
                        InvokePublicationMoveHook(backupRootPath, destinationRootPath);
                        EditorDirectoryPublicationMove.Move(backupRootPath, destinationRootPath);
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
                    EditorDirectoryPublicationMove.Move(stagingRootPath, destinationRootPath);
                } else if (Directory.Exists(backupRootPath)) {
                    EditorDirectoryPublicationMove.Move(backupRootPath, destinationRootPath);
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
    }
}
