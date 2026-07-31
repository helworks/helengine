namespace helengine {
    /// <summary>
    /// Supplies deterministic scene roots while applying one test mutation immediately before binding begins.
    /// </summary>
    sealed class MutatingRootEntityList : IReadOnlyList<Entity> {
        /// <summary>
        /// Stores the deterministic roots returned by the indexer.
        /// </summary>
        readonly Entity[] Entities;

        /// <summary>
        /// Stores the mutation applied on the first index access after the complete validation pass.
        /// </summary>
        readonly Action Mutation;

        /// <summary>
        /// Stores how many roots have been requested across validation and binding passes.
        /// </summary>
        int AccessCount;

        /// <summary>
        /// Initializes a root list that mutates external entity state between binder validation and mutation passes.
        /// </summary>
        /// <param name="entities">Deterministic roots exposed by the list.</param>
        /// <param name="mutation">Mutation applied immediately before the first binding-pass root is returned.</param>
        public MutatingRootEntityList(Entity[] entities, Action mutation) {
            Entities = entities ?? throw new ArgumentNullException(nameof(entities));
            Mutation = mutation ?? throw new ArgumentNullException(nameof(mutation));
        }

        /// <summary>
        /// Gets the number of deterministic scene roots exposed by this list.
        /// </summary>
        public int Count => Entities.Length;

        /// <summary>
        /// Gets one root and applies the configured mutation at the validation-to-binding boundary.
        /// </summary>
        /// <param name="index">Root index requested by the binder.</param>
        /// <returns>The deterministic root stored at the supplied index.</returns>
        public Entity this[int index] {
            get {
                if (AccessCount == Entities.Length) {
                    Mutation();
                }

                AccessCount++;
                return Entities[index];
            }
        }

        /// <summary>
        /// Creates the generic enumerator required by the read-only list contract.
        /// </summary>
        /// <returns>An enumerator over the stored roots.</returns>
        public IEnumerator<Entity> GetEnumerator() {
            return ((IEnumerable<Entity>)Entities).GetEnumerator();
        }

        /// <summary>
        /// Creates the non-generic enumerator required by the enumerable contract.
        /// </summary>
        /// <returns>An enumerator over the stored roots.</returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return Entities.GetEnumerator();
        }
    }
}
