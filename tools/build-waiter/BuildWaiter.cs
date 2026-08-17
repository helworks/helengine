using System.Diagnostics;

namespace helengine.tools.buildwaiter {
    /// <summary>
    /// Launches one platform build command, forwards its diagnostics, and verifies current successful state and published artifacts before reporting completion.
    /// </summary>
    public sealed class BuildWaiter {
        /// <summary>
        /// Interval between status messages while a child build process remains active.
        /// </summary>
        static readonly TimeSpan WaitingStatusInterval = TimeSpan.FromSeconds(10);

        readonly BuildVerificationHandshake VerificationHandshake;

        /// <summary>
        /// Initializes one build waiter with the active-child verification handshake.
        /// </summary>
        /// <param name="verificationHandshake">Coordinator for proof verification and wrapper release.</param>
        public BuildWaiter(BuildVerificationHandshake verificationHandshake) {
            VerificationHandshake = verificationHandshake ?? throw new ArgumentNullException(nameof(verificationHandshake));
        }

        /// <summary>
        /// Launches the configured build command and waits until it fails or produces successful current state and every required current artifact.
        /// </summary>
        /// <param name="options">Command and output-artifact contract for the build invocation.</param>
        /// <param name="cancellationToken">Cancellation token used while waiting for the child build process.</param>
        /// <returns>Terminal build result containing child-process, state-verification, or artifact-verification status.</returns>
        public async Task<BuildWaiterResult> WaitAsync(BuildWaiterOptions options, CancellationToken cancellationToken) {
            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            DateTime buildStartedUtc = DateTime.UtcNow;
            string invocationId = Guid.NewGuid().ToString("D");
            ProcessStartInfo startInfo = new ProcessStartInfo {
                FileName = options.CommandFileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            startInfo.Environment["HELENGINE_BUILD_INVOCATION_ID"] = invocationId;
            startInfo.Environment["HELENGINE_BUILD_WAITER_PROTOCOL"] = "ack-v1";
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
            try {
                if (!process.Start()) {
                    return new BuildWaiterResult(false, 1, $"Build command '{options.CommandFileName}' did not start.");
                }
            } catch (System.ComponentModel.Win32Exception exception) {
                return new BuildWaiterResult(false, 1, $"Build command '{options.CommandFileName}' did not start: {exception.Message}");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            Task processExitTask = process.WaitForExitAsync(CancellationToken.None);
            Task<BuildVerificationHandshakeResult> handshakeTask = VerificationHandshake.VerifyAndAcknowledgeAsync(
                options.OutputRootPath,
                options.RequiredArtifactRelativePaths,
                buildStartedUtc,
                invocationId,
                processExitTask);
            while (!processExitTask.IsCompleted) {
                Task waitingStatusTask = Task.Delay(WaitingStatusInterval);
                Task completedTask = await Task.WhenAny(processExitTask, waitingStatusTask);
                if (completedTask == waitingStatusTask && !processExitTask.IsCompleted) {
                    Console.WriteLine("[build-waiter] waiting for build process completion");
                }
            }

            await processExitTask;
            await Task.WhenAll(standardOutputCompleted.Task, standardErrorCompleted.Task);
            if (process.ExitCode != 0) {
                try {
                    await handshakeTask;
                } catch (Exception) {
                }
                return new BuildWaiterResult(false, process.ExitCode, $"Build command exited with code {process.ExitCode}.");
            }

            BuildVerificationHandshakeResult handshake;
            try {
                handshake = await handshakeTask;
            } catch (Exception exception) {
                return new BuildWaiterResult(false, 1, $"Build verification handshake failed: {exception.Message}");
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(handshake.AcknowledgementFailureMessage)) {
                return new BuildWaiterResult(false, 1, handshake.AcknowledgementFailureMessage);
            }
            if (!handshake.StateVerificationResult.Succeeded) {
                return new BuildWaiterResult(false, 1, handshake.StateVerificationResult.Message);
            }
            if (!handshake.ArtifactVerificationResult.Succeeded) {
                return new BuildWaiterResult(false, 1, handshake.ArtifactVerificationResult.Message);
            }
            return new BuildWaiterResult(true, 0, handshake.ArtifactVerificationResult.Message);
        }
    }
}
