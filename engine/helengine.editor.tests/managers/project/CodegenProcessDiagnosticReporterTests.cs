namespace helengine.editor.tests;

/// <summary>
/// Verifies generated-core codegen diagnostics are written immediately and describe a running child process.
/// </summary>
public sealed class CodegenProcessDiagnosticReporterTests : IDisposable {
    /// <summary>
    /// Temporary directory containing the per-build regeneration log used by the test.
    /// </summary>
    readonly string RootPath;

    /// <summary>
    /// Initializes an isolated regeneration-log directory for one test instance.
    /// </summary>
    public CodegenProcessDiagnosticReporterTests() {
        RootPath = Path.Combine(Path.GetTempPath(), "helengine-codegen-diagnostics-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    /// <summary>
    /// Verifies process startup, live output, and heartbeat records reach their respective output streams without waiting for process completion.
    /// </summary>
    [Fact]
    public void Reporter_when_process_is_running_writes_live_console_and_log_records() {
        string logPath = Path.Combine(RootPath, "generated-core-regeneration.log");
        using StringWriter standardOutput = new();
        using StringWriter standardError = new();
        using CodegenProcessDiagnosticReporter reporter = new(
            "C:/engine/helengine.core/helengine.core.csproj",
            logPath,
            standardOutput.WriteLine,
            standardError.WriteLine);

        reporter.ReportProcessStarted(4242);
        reporter.ReportOutputLine("generated Core.cpp", false);
        reporter.ReportOutputLine("warning from codegen", true);
        reporter.ReportHeartbeat();

        Assert.Contains("pid=4242", standardOutput.ToString(), StringComparison.Ordinal);
        Assert.Contains("generated Core.cpp", standardOutput.ToString(), StringComparison.Ordinal);
        Assert.Contains("warning from codegen", standardError.ToString(), StringComparison.Ordinal);
        Assert.Contains("heartbeat", standardOutput.ToString(), StringComparison.Ordinal);
        string log = File.ReadAllText(logPath);
        Assert.Contains("generated Core.cpp", log, StringComparison.Ordinal);
        Assert.Contains("warning from codegen", log, StringComparison.Ordinal);
        Assert.Contains("heartbeat", log, StringComparison.Ordinal);
    }

    /// <summary>
    /// Deletes the test-owned regeneration log directory.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(RootPath)) {
            Directory.Delete(RootPath, true);
        }
    }
}
