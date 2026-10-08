namespace helengine.vfx {
    /// <summary>
    /// Knows how many constant slots and value components each <see cref="EffectParameterType"/> occupies, so the
    /// validator, the slot resolver and capability descriptions agree on one packing rule.
    /// </summary>
    public static class VfxParameterLayout {
        /// <summary>
        /// Returns the number of consecutive constant slots a parameter of the given type occupies.
        /// </summary>
        /// <param name="type">Parameter value shape.</param>
        /// <returns>Slot count from one to four.</returns>
        public static int SlotCount(EffectParameterType type) {
            if (type == EffectParameterType.Float2) {
                return 2;
            } else if (type == EffectParameterType.Float4 || type == EffectParameterType.Color) {
                return 4;
            }
            return 1;
        }

        /// <summary>
        /// Reports whether the type stores a whole number (integers, switches and enum indices).
        /// </summary>
        /// <param name="type">Parameter value shape.</param>
        /// <returns>True when every stored component must be a whole number.</returns>
        public static bool IsWholeNumber(EffectParameterType type) {
            return type == EffectParameterType.Integer || type == EffectParameterType.Bool || type == EffectParameterType.Enum;
        }
    }
}
