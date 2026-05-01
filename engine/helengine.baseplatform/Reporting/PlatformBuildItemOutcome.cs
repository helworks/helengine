namespace helengine.baseplatform.Reporting;

/// <summary>
/// Records the final outcome for one built scene or loose asset.
/// </summary>
public class PlatformBuildItemOutcome {
    /// <summary>
    /// Initializes a new item outcome record.
    /// </summary>
    /// <param name="itemId">The identifier of the scene or asset.</param>
    /// <param name="outcomeKind">The final outcome kind.</param>
    public PlatformBuildItemOutcome(string itemId, PlatformBuildItemOutcomeKind outcomeKind) {
        if (string.IsNullOrWhiteSpace(itemId)) {
            throw new ArgumentException("Item id is required.", nameof(itemId));
        }

        ItemId = itemId;
        OutcomeKind = outcomeKind;
    }

    /// <summary>
    /// Gets the identifier of the scene or asset this outcome describes.
    /// </summary>
    public string ItemId { get; }

    /// <summary>
    /// Gets the final outcome kind for the scene or asset.
    /// </summary>
    public PlatformBuildItemOutcomeKind OutcomeKind { get; }
}
