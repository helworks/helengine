namespace helengine.baseplatform.Reporting;

/// <summary>
/// Describes a progress update emitted while the builder processes scenes or assets.
/// </summary>
public class PlatformBuildProgressUpdate {
    /// <summary>
    /// Initializes a new progress update with explicit stage and progress counts.
    /// </summary>
    /// <param name="stageName">The logical build stage currently executing.</param>
    /// <param name="currentItemIdentity">The current item identity being processed.</param>
    /// <param name="completedCount">The number of completed items for the current stage.</param>
    /// <param name="totalCount">The total number of items for the current stage.</param>
    /// <param name="message">The human-readable progress message.</param>
    public PlatformBuildProgressUpdate(
        string stageName,
        string currentItemIdentity,
        int completedCount,
        int totalCount,
        string message) {
        if (string.IsNullOrWhiteSpace(stageName)) {
            throw new ArgumentException("Stage name is required.", nameof(stageName));
        } else if (string.IsNullOrWhiteSpace(currentItemIdentity)) {
            throw new ArgumentException("Current item identity is required.", nameof(currentItemIdentity));
        } else if (completedCount < 0) {
            throw new ArgumentOutOfRangeException(nameof(completedCount), "Completed count cannot be negative.");
        } else if (totalCount < 0) {
            throw new ArgumentOutOfRangeException(nameof(totalCount), "Total count cannot be negative.");
        } else if (completedCount > totalCount) {
            throw new ArgumentException("Completed count cannot exceed total count.", nameof(completedCount));
        } else if (string.IsNullOrWhiteSpace(message)) {
            throw new ArgumentException("Progress message is required.", nameof(message));
        }

        StageName = stageName;
        CurrentItemIdentity = currentItemIdentity;
        CompletedCount = completedCount;
        TotalCount = totalCount;
        Message = message;
    }

    /// <summary>
    /// Gets the logical build stage currently executing.
    /// </summary>
    public string StageName { get; }

    /// <summary>
    /// Gets the identity of the current scene or asset being processed.
    /// </summary>
    public string CurrentItemIdentity { get; }

    /// <summary>
    /// Gets the number of items completed for the current stage.
    /// </summary>
    public int CompletedCount { get; }

    /// <summary>
    /// Gets the total number of items in the current stage.
    /// </summary>
    public int TotalCount { get; }

    /// <summary>
    /// Gets the human-readable progress message.
    /// </summary>
    public string Message { get; }
}
