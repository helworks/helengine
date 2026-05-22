namespace helengine {
    /// <summary>
    /// Stores the active state of one conditional-preprocessor branch while shader source is being reduced for binding parsing.
    /// </summary>
    public class ShaderConditionalFrame {
        /// <summary>
        /// Initializes one conditional-preprocessor branch frame.
        /// </summary>
        /// <param name="parentIncluded">Whether all parent branches are currently active.</param>
        /// <param name="branchMatched">Whether any branch in the current conditional block has already matched.</param>
        /// <param name="currentIncluded">Whether the current branch body should be included in the preprocessed output.</param>
        public ShaderConditionalFrame(bool parentIncluded, bool branchMatched, bool currentIncluded) {
            ParentIncluded = parentIncluded;
            BranchMatched = branchMatched;
            CurrentIncluded = currentIncluded;
        }

        /// <summary>
        /// Gets whether all parent conditional branches are active.
        /// </summary>
        public bool ParentIncluded { get; }

        /// <summary>
        /// Gets or sets whether any branch inside the current conditional block has already matched.
        /// </summary>
        public bool BranchMatched { get; set; }

        /// <summary>
        /// Gets or sets whether the current branch body should be emitted.
        /// </summary>
        public bool CurrentIncluded { get; set; }

        /// <summary>
        /// Gets or sets whether an <c>#else</c> branch has already been encountered for the current block.
        /// </summary>
        public bool HasElseBranch { get; set; }
    }
}
