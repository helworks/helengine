namespace helengine.vfx {
    /// <summary>
    /// Pure easing curve math. Must stay in sync with the identical formulas in VfxCommon.hlsli.
    /// </summary>
    public static class VfxEasing {
        /// <summary>
        /// Applies an easing curve to a normalized progress value.
        /// </summary>
        /// <param name="kind">Curve to apply.</param>
        /// <param name="t">Normalized progress; values outside [0, 1] are clamped before the curve is applied.</param>
        /// <returns>Eased progress in [0, 1].</returns>
        public static float Apply(VfxEasingKind kind, float t) {
            float clamped = Math.Clamp(t, 0f, 1f);
            switch (kind) {
                case VfxEasingKind.Linear:
                    return clamped;
                case VfxEasingKind.EaseIn:
                    return clamped * clamped;
                case VfxEasingKind.EaseOut:
                    return 1f - ((1f - clamped) * (1f - clamped));
                case VfxEasingKind.EaseInOut:
                    return clamped < 0.5f
                        ? 2f * clamped * clamped
                        : 1f - (float)(Math.Pow((-2f * clamped) + 2f, 2) / 2f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown easing kind.");
            }
        }
    }
}
