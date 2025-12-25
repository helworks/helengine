namespace helengine {
    /// <summary>
    /// Represents a deferred update bucket change requested during an active update pass.
    /// </summary>
    public sealed class PendingUpdateOperation {
        /// <summary>
        /// Initializes a new pending update operation.
        /// </summary>
        /// <param name="entity">Updateable affected by the operation.</param>
        /// <param name="isAdd">True to add the updateable; false to remove it.</param>
        /// <param name="updateOrder">Update order captured when the operation was queued.</param>
        public PendingUpdateOperation(IUpdateable entity, bool isAdd, byte updateOrder) {
            Entity = entity;
            IsAdd = isAdd;
            UpdateOrder = updateOrder;
        }

        /// <summary>
        /// Gets the updateable affected by this operation.
        /// </summary>
        public IUpdateable Entity { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this is an add operation.
        /// </summary>
        public bool IsAdd { get; private set; }

        /// <summary>
        /// Gets or sets the update order captured when the operation was queued.
        /// </summary>
        public byte UpdateOrder { get; set; }
    }
}
