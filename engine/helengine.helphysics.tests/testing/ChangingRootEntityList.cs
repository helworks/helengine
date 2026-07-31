namespace helengine {
    /// <summary>
    /// Returns one deterministic root set during validation and a different set if a caller rereads the indexer afterward.
    /// </summary>
    sealed class ChangingRootEntityList : IReadOnlyList<Entity> {
        /// <summary>
        /// Stores the roots that a binding transaction must validate and retain as its authoritative snapshot.
        /// </summary>
        readonly Entity[] ValidatedEntities;

        /// <summary>
        /// Stores replacement roots exposed only by index accesses beyond the first complete pass.
        /// </summary>
        readonly Entity[] ReplacementEntities;

        /// <summary>
        /// Stores how many roots have been requested across all indexer passes.
        /// </summary>
        int AccessCount;

        /// <summary>
        /// Initializes equal-length validation and replacement root sets.
        /// </summary>
        /// <param name="validatedEntities">Roots returned during the first complete indexer pass.</param>
        /// <param name="replacementEntities">Roots returned if the caller performs a later pass.</param>
        /// <exception cref="ArgumentNullException">Thrown when either root array is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the arrays have different lengths.</exception>
        public ChangingRootEntityList(Entity[] validatedEntities, Entity[] replacementEntities) {
            ValidatedEntities = validatedEntities ?? throw new ArgumentNullException(nameof(validatedEntities));
            ReplacementEntities = replacementEntities ?? throw new ArgumentNullException(nameof(replacementEntities));
            if (ValidatedEntities.Length != ReplacementEntities.Length) {
                throw new ArgumentException("Changing root test lists require equal validation and replacement counts.", nameof(replacementEntities));
            }
        }

        /// <summary>
        /// Gets the stable number of roots exposed by both configured passes.
        /// </summary>
        public int Count => ValidatedEntities.Length;

        /// <summary>
        /// Gets a validation root during the first pass or a replacement root during any later pass.
        /// </summary>
        /// <param name="index">Root index requested by the binder.</param>
        /// <returns>The root belonging to the current indexer pass.</returns>
        public Entity this[int index] {
            get {
                Entity entity = AccessCount < ValidatedEntities.Length
                    ? ValidatedEntities[index]
                    : ReplacementEntities[index];
                AccessCount++;
                return entity;
            }
        }

        /// <summary>
        /// Creates the generic enumerator required by the read-only list contract.
        /// </summary>
        /// <returns>An enumerator over the validation roots.</returns>
        public IEnumerator<Entity> GetEnumerator() {
            return ((IEnumerable<Entity>)ValidatedEntities).GetEnumerator();
        }

        /// <summary>
        /// Creates the non-generic enumerator required by the enumerable contract.
        /// </summary>
        /// <returns>An enumerator over the validation roots.</returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return ValidatedEntities.GetEnumerator();
        }
    }
}
