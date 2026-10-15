namespace helengine.editor.automation;

/// <summary>
/// Holds bounded resource limits shared by editor automation hosts and their tests.
/// </summary>
public sealed class EditorAutomationLimits
{
    /// <summary>Creates limits with the contract's production-safe defaults.</summary>
    public EditorAutomationLimits() : this(100, 500, 1024, 64, 10000, 4 * 1024 * 1024, 10000)
    {
    }

    /// <summary>Creates injectable positive limits for deterministic boundary tests.</summary>
    /// <param name="DefaultPageSize">Number of records requested when a caller omits a page size.</param>
    /// <param name="MaximumPageSize">Largest page a caller may request.</param>
    /// <param name="MaximumPendingCalls">Maximum concurrent requests admitted by an instance.</param>
    /// <param name="MaximumBindings">Maximum live client bindings in one editor instance.</param>
    /// <param name="MaximumOperationIdsPerBinding">Maximum retained mutation identities per binding.</param>
    /// <param name="MaximumRequestBytes">Maximum accepted request body size.</param>
    /// <param name="MaximumRetainedLogEntries">Maximum log entries retained by an instance.</param>
    public EditorAutomationLimits(int DefaultPageSize, int MaximumPageSize, int MaximumPendingCalls, int MaximumBindings, int MaximumOperationIdsPerBinding, int MaximumRequestBytes, int MaximumRetainedLogEntries)
    {
        if (DefaultPageSize <= 0 || MaximumPageSize < DefaultPageSize || MaximumPendingCalls <= 0 || MaximumBindings <= 0 || MaximumOperationIdsPerBinding <= 0 || MaximumRequestBytes <= 0 || MaximumRetainedLogEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(DefaultPageSize), "Every limit must be positive and the maximum page must include the default page size.");
        }

        this.DefaultPageSize = DefaultPageSize;
        this.MaximumPageSize = MaximumPageSize;
        this.MaximumPendingCalls = MaximumPendingCalls;
        this.MaximumBindings = MaximumBindings;
        this.MaximumOperationIdsPerBinding = MaximumOperationIdsPerBinding;
        this.MaximumRequestBytes = MaximumRequestBytes;
        this.MaximumRetainedLogEntries = MaximumRetainedLogEntries;
    }

    /// <summary>Gets the page size used when a caller does not supply a limit.</summary>
    public int DefaultPageSize { get; }

    /// <summary>Gets the upper bound applied to a requested page size.</summary>
    public int MaximumPageSize { get; }

    /// <summary>Gets the maximum number of accepted but unfinished calls.</summary>
    public int MaximumPendingCalls { get; }

    /// <summary>Gets the maximum number of active editor bindings.</summary>
    public int MaximumBindings { get; }

    /// <summary>Gets the maximum mutation idempotency records retained for one binding.</summary>
    public int MaximumOperationIdsPerBinding { get; }

    /// <summary>Gets the maximum size, in bytes, of a request body.</summary>
    public int MaximumRequestBytes { get; }

    /// <summary>Gets the maximum count of log records retained for pagination.</summary>
    public int MaximumRetainedLogEntries { get; }
}
