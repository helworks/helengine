namespace helengine {
    /// <summary>
    /// Represents a deferred update list change requested during an active update pass.
    /// </summary>
    public sealed class PendingUpdateOperation {
        /// <summary>
        /// Initializes a new pending update operation.
        /// </summary>
        /// <param name="entity">Updateable affected by the operation.</param>
        /// <param name="isAdd">True to add the updateable; false to remove it.</param>
        public PendingUpdateOperation(IUpdateable entity, bool isAdd) {
            Entity = entity;
            IsAdd = isAdd;
        }

        /// <summary>
        /// Gets the updateable affected by this operation.
        /// </summary>
        public IUpdateable Entity { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this is an add operation.
        /// </summary>
        public bool IsAdd { get; private set; }
    }
}
