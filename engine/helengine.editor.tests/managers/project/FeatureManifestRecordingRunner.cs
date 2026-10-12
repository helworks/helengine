namespace helengine.editor.tests.managers.project;

/// <summary>Captures the official feature producer invocation and the report it receives without modifying generated files.</summary>
internal sealed class FeatureManifestRecordingRunner : IEditorCodegenToolRunner {
    /// <summary>Number of producer invocations made by the composer.</summary>
    public int InvocationCount { get; private set; }

    /// <summary>Selected converter executable supplied by the build graph.</summary>
    public string ToolPath { get; private set; }

    /// <summary>Exact argument sequence forwarded to the official producer.</summary>
    public IReadOnlyList<string> Arguments { get; private set; }

    /// <summary>Project directory retained as the converter working directory.</summary>
    public string WorkingDirectory { get; private set; }

    /// <summary>Combined report read at invocation time, after all project reports have merged.</summary>
    public string ReportContents { get; private set; }

    /// <summary>Optional failure thrown to verify that producer errors remain visible to the caller.</summary>
    public Exception Failure { get; set; }

    /// <summary>Records the producer arguments and reads its report input, then propagates any configured failure.</summary>
    public void Run(string toolPath, IReadOnlyList<string> arguments, string workingDirectory) {
        InvocationCount++;
        ToolPath = toolPath;
        Arguments = arguments.ToArray();
        WorkingDirectory = workingDirectory;
        int reportIndex = Array.IndexOf(Arguments.ToArray(), "--report");
        ReportContents = File.ReadAllText(Arguments[reportIndex + 1]);
        if (Failure != null) {
            throw Failure;
        }
    }
}
