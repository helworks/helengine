namespace helengine {
    /// <summary>
    /// Describes a component that writes scaled layout values into the entity subtree it owns while the authored, unscaled
    /// values remain the persisted source of truth. Editor serialization restores the authored values before reading the
    /// subtree and re-applies the scale afterwards.
    /// </summary>
    public interface IAuthoredLayoutOwner {
        /// <summary>
        /// Writes the authored layout back onto the owned subtree so it can be serialized as authored data. In the editor, a
        /// live value that differs from the value the last scale pass wrote is treated as a user edit made in the scaled
        /// preview and is mapped back into authored space. Outside the editor this does nothing.
        /// </summary>
        void RestoreAuthoredLayout();

        /// <summary>
        /// Re-applies the current scale to the owned subtree after <see cref="RestoreAuthoredLayout"/>.
        /// </summary>
        void ReapplyLayout();
    }
}
