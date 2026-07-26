using System.Diagnostics;

namespace helengine.editor {
    /// <summary>
    /// Streams one generated-core codegen process's output and periodic liveness records to the build console and regeneration log.
    /// </summary>
    internal sealed class CodegenProcessDiagnosticReporter : IDisposable {
        /// <summary>
        /// Interval between liveness records while codegen has not produced another output line.
        /// </summary>
        static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Absolute project path currently being converted by codegen.
        /// </summary>
        readonly string ProjectPath;

        /// <summary>
        /// Absolute path of the regeneration log that receives the same records as the console.
        /// </summary>
        readonly string RegenerationLogPath;

        /// <summary>
        /// Console output sink for standard output and lifecycle records.
        /// </summary>
        readonly Action<string> StandardOutputWriter;

        /// <summary>
        /// Console output sink for codegen standard-error records.
        /// </summary>
        readonly Action<string> StandardErrorWriter;

        /// <summary>
        /// Synchronizes concurrent child-output and timer callbacks.
        /// </summary>
        readonly object WriteLock = new();

        /// <summary>
        /// Measures the current child process's elapsed run time.
        /// </summary>
        readonly Stopwatch Elapsed = new();

        /// <summary>
        /// Schedules periodic liveness records while codegen remains active.
        /// </summary>
        Timer HeartbeatTimer;

        /// <summary>
        /// Identifier of the active codegen child process.
        /// </summary>
        int ProcessId;

        /// <summary>
        /// Initializes one reporter for a single generated-core codegen project.
        /// </summary>
        /// <param name="projectPath">Absolute path of the project being converted.</param>
        /// <param name="regenerationLogPath">Absolute path of the persistent regeneration log.</param>
        /// <param name="standardOutputWriter">Sink for standard-output and lifecycle records.</param>
        /// <param name="standardErrorWriter">Sink for standard-error records.</param>
        public CodegenProcessDiagnosticReporter(
            string projectPath,
            string regenerationLogPath,
            Action<string> standardOutputWriter,
            Action<string> standardErrorWriter) {
            ProjectPath = string.IsNullOrWhiteSpace(projectPath)
                ? throw new ArgumentException("Project path must be provided.", nameof(projectPath))
                : projectPath;
            RegenerationLogPath = string.IsNullOrWhiteSpace(regenerationLogPath)
                ? throw new ArgumentException("Regeneration log path must be provided.", nameof(regenerationLogPath))
                : regenerationLogPath;
            StandardOutputWriter = standardOutputWriter ?? throw new ArgumentNullException(nameof(standardOutputWriter));
            StandardErrorWriter = standardErrorWriter ?? throw new ArgumentNullException(nameof(standardErrorWriter));
        }

        /// <summary>
        /// Starts liveness reporting after the codegen child process has launched.
        /// </summary>
        /// <param name="processId">Identifier assigned to the launched codegen process.</param>
        public void ReportProcessStarted(int processId) {
            if (processId <= 0) {
                throw new ArgumentOutOfRangeException(nameof(processId));
            }

            lock (WriteLock) {
                ProcessId = processId;
                Elapsed.Restart();
                WriteRecord($"[codegen] started project={ProjectPath} pid={ProcessId}", false);
                HeartbeatTimer = new Timer(_ => ReportHeartbeat(), null, HeartbeatInterval, HeartbeatInterval);
            }
        }

        /// <summary>
        /// Immediately forwards one codegen output line to its matching console stream and the regeneration log.
        /// </summary>
        /// <param name="line">Complete output line emitted by codegen.</param>
        /// <param name="isError">Whether the line originated from standard error.</param>
        public void ReportOutputLine(string line, bool isError) {
            if (line == null) {
                throw new ArgumentNullException(nameof(line));
            }

            lock (WriteLock) {
                WriteRecord($"[codegen] project={ProjectPath} {line}", isError);
            }
        }

        /// <summary>
        /// Immediately writes a liveness record for a still-running codegen child process.
        /// </summary>
        public void ReportHeartbeat() {
            lock (WriteLock) {
                if (ProcessId <= 0) {
                    return;
                }

                WriteRecord($"[codegen] heartbeat project={ProjectPath} pid={ProcessId} elapsed={Elapsed.Elapsed:hh\\:mm\\:ss}", false);
            }
        }

        /// <summary>
        /// Stops periodic liveness reporting after the child process exits or regeneration unwinds.
        /// </summary>
        public void Dispose() {
            lock (WriteLock) {
                HeartbeatTimer?.Dispose();
                HeartbeatTimer = null;
                Elapsed.Stop();
                ProcessId = 0;
            }
        }

        /// <summary>
        /// Mirrors one diagnostic record to its selected console stream and the persistent regeneration log.
        /// </summary>
        /// <param name="message">Formatted diagnostic record to emit.</param>
        /// <param name="isError">Whether the record belongs on standard error.</param>
        void WriteRecord(string message, bool isError) {
            if (isError) {
                StandardErrorWriter(message);
            } else {
                StandardOutputWriter(message);
            }

            EditorGeneratedCoreRegenerationService.AppendRegenerationLog(RegenerationLogPath, message);
        }
    }
}
