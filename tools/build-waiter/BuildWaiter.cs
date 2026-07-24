using System.Diagnostics;

namespace helengine.tools.buildwaiter {
    /// <summary>
    /// Launches one platform build command, forwards its diagnostics, and verifies the build's published artifacts before reporting completion.
    /// </summary>
    public sealed class BuildWaiter {
        /// <summary>
        /// Interval between status messages while a child build process remains active.
        /// </summary>
        static readonly TimeSpan WaitingStatusInterval = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Verifier that determines whether the child build produced the current invocation's required output files.
        /// </summary>
        readonly BuildArtifactVerifier ArtifactVerifier;

        /// <summary>
        /// Initializes one build waiter with the artifact verifier used after child-process completion.
        /// </summary>
        /// <param name="artifactVerifier">Verifier for the final published output artifacts.</param>
        public BuildWaiter(BuildArtifactVerifier artifactVerifier) {
            ArtifactVerifier = artifactVerifier ?? throw new ArgumentNullException(nameof(artifactVerifier));
        }

        /// <summary>
        /// Launches the configured build command and waits until it fails or produces every required current artifact.
        /// </summary>
        /// <param name="options">Command and output-artifact contract for the build invocation.</param>
        /// <param name="cancellationToken">Cancellation token used while waiting for the child build process.</param>
        /// <returns>Terminal build result containing child-process or artifact-verification status.</returns>
        public async Task<BuildWaiterResult> WaitAsync(BuildWaiterOptions options, CancellationToken cancellationToken) {
            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            DateTime buildStartedUtc = DateTime.UtcNow;
            ProcessStartInfo startInfo = new ProcessStartInfo {
                FileName = options.CommandFileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            for (int argumentIndex = 0; argumentIndex < options.CommandArguments.Length; argumentIndex++) {
                startInfo.ArgumentList.Add(options.CommandArguments[argumentIndex]);
            }

            using Process process = new Process {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            TaskCompletionSource standardOutputCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource standardErrorCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            process.OutputDataReceived += (_, eventArgs) => {
                if (eventArgs.Data == null) {
                    standardOutputCompleted.TrySetResult();
                } else {
                    Console.WriteLine(eventArgs.Data);
                }
            };
            process.ErrorDataReceived += (_, eventArgs) => {
                if (eventArgs.Data == null) {
                    standardErrorCompleted.TrySetResult();
                } else {
                    Console.Error.WriteLine(eventArgs.Data);
                }
            };

            Console.WriteLine($"[build-waiter] launching: {options.CommandFileName}");
            if (!process.Start()) {
                return new BuildWaiterResult(false, 1, $"Build command '{options.CommandFileName}' did not start.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            Task processExitTask = process.WaitForExitAsync(cancellationToken);
            while (!processExitTask.IsCompleted) {
                Task waitingStatusTask = Task.Delay(WaitingStatusInterval, cancellationToken);
                Task completedTask = await Task.WhenAny(processExitTask, waitingStatusTask);
                if (completedTask == waitingStatusTask && !processExitTask.IsCompleted) {
                    Console.WriteLine("[build-waiter] waiting for build process completion");
                }
            }

            await processExitTask;
            await Task.WhenAll(standardOutputCompleted.Task, standardErrorCompleted.Task);
            if (process.ExitCode != 0) {
                return new BuildWaiterResult(false, process.ExitCode, $"Build command exited with code {process.ExitCode}.");
            }

            BuildArtifactVerificationResult verificationResult = ArtifactVerifier.Verify(
                options.OutputRootPath,
                options.RequiredArtifactRelativePaths,
                buildStartedUtc);
            if (!verificationResult.Succeeded) {
                return new BuildWaiterResult(false, 1, verificationResult.Message);
            }

            return new BuildWaiterResult(true, 0, verificationResult.Message);
        }
    }
}
