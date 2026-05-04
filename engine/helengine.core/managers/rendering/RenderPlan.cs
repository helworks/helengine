namespace helengine {
    /// <summary>
    /// Stores the backend-neutral pass list selected for one render frame.
    /// </summary>
    public class RenderPlan {
        /// <summary>
        /// Initializes one render plan.
        /// </summary>
        /// <param name="passes">Pass kinds selected for the frame.</param>
        public RenderPlan(IReadOnlyList<RenderPassKind> passes) {
            Passes = passes ?? throw new ArgumentNullException(nameof(passes));
        }

        /// <summary>
        /// Gets the ordered pass kinds selected for the frame.
        /// </summary>
        public IReadOnlyList<RenderPassKind> Passes { get; }
    }
}
