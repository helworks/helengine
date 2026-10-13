namespace helengine.player {
    /// <summary>
    /// Binds a pair of keyboard keys to one stick axis of the keyboard-emulated pad: each key deflects the axis fully
    /// in its direction, and holding both leaves it centered.
    /// </summary>
    public sealed class PlayerKeyboardGamepadAxisBinding {
        /// <summary>
        /// Initializes one key-pair-to-axis binding.
        /// </summary>
        /// <param name="negativeKey">Key that deflects the axis fully negative (left or up).</param>
        /// <param name="positiveKey">Key that deflects the axis fully positive (right or down).</param>
        /// <param name="axis">Stick axis the keys deflect.</param>
        public PlayerKeyboardGamepadAxisBinding(Keys negativeKey, Keys positiveKey, PlayerGamepadAxis axis) {
            NegativeKey = negativeKey;
            PositiveKey = positiveKey;
            Axis = axis;
        }

        /// <summary>
        /// Gets the key that deflects the axis fully negative (left or up).
        /// </summary>
        public Keys NegativeKey { get; }

        /// <summary>
        /// Gets the key that deflects the axis fully positive (right or down).
        /// </summary>
        public Keys PositiveKey { get; }

        /// <summary>
        /// Gets the stick axis the keys deflect.
        /// </summary>
        public PlayerGamepadAxis Axis { get; }
    }
}
