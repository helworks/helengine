namespace helengine.directx11 {
    /// <summary>
    /// Chooses the visible lights that survive the DirectX11 light budget for one render frame.
    /// </summary>
    public sealed class DirectX11LightSelectionService {
        /// <summary>
        /// Selects the visible lights that survive the DirectX11 light budget.
        /// </summary>
        /// <param name="visibleLights">Visible light submissions gathered for the frame.</param>
        /// <param name="maximumVisibleLights">Maximum number of visible lights allowed by the backend.</param>
        /// <returns>Ordered light submissions that survive the budget.</returns>
        public RenderFrameLightSubmission[] SelectVisibleLights(IReadOnlyList<RenderFrameLightSubmission> visibleLights, int maximumVisibleLights) {
            if (visibleLights == null) {
                throw new ArgumentNullException(nameof(visibleLights));
            }
            if (maximumVisibleLights < 0) {
                throw new ArgumentOutOfRangeException(nameof(maximumVisibleLights), "Maximum visible lights must be non-negative.");
            }

            List<RenderFrameLightSubmission> selectedLights = new List<RenderFrameLightSubmission>(visibleLights.Count);
            for (int index = 0; index < visibleLights.Count; index++) {
                selectedLights.Add(visibleLights[index]);
            }

            selectedLights.Sort((left, right) => right.Importance.CompareTo(left.Importance));
            if (selectedLights.Count <= maximumVisibleLights) {
                return selectedLights.ToArray();
            }

            return selectedLights.GetRange(0, maximumVisibleLights).ToArray();
        }
    }
}
