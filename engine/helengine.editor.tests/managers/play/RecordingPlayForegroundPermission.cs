namespace helengine.editor.tests.managers.play;

/// <summary>
/// Test double for the foreground grant that records every process it was asked about and answers with a fixed
/// outcome, so launcher tests never touch the real Win32 foreground state.
/// </summary>
public sealed class RecordingPlayForegroundPermission : IEditorPlayForegroundPermission {
    /// <summary>
    /// Process identifiers the launcher asked to grant, in call order.
    /// </summary>
    public readonly List<int> RequestedProcessIds = new List<int>();

    /// <summary>
    /// Whether each grant is accepted.
    /// </summary>
    readonly bool Accept;

    /// <summary>
    /// Error code reported for a refused grant.
    /// </summary>
    readonly int RefusalErrorCode;

    /// <summary>
    /// Initializes the recorder with the outcome every grant reports.
    /// </summary>
    /// <param name="accept">True to accept every grant; false to refuse it.</param>
    /// <param name="refusalErrorCode">Win32 error code reported when a grant is refused.</param>
    public RecordingPlayForegroundPermission(bool accept, int refusalErrorCode) {
        Accept = accept;
        RefusalErrorCode = refusalErrorCode;
    }

    /// <summary>
    /// Records the process identifier and reports the configured outcome.
    /// </summary>
    /// <param name="processId">Process the launcher asked to grant.</param>
    /// <param name="errorCode">0 when accepted; the configured refusal code otherwise.</param>
    /// <returns>The configured outcome.</returns>
    public bool TryAllow(int processId, out int errorCode) {
        RequestedProcessIds.Add(processId);
        errorCode = Accept ? 0 : RefusalErrorCode;
        return Accept;
    }
}
