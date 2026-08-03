namespace helengine.vfx {
    /// <summary>
    /// Describes the fixed constant-buffer layout every VFX effect shader shares: normalized time,
    /// resolution, and a fixed bank of parameter slots. Must stay in sync with the shared HLSL
    /// cbuffer declaration in VfxCommon.hlsli (register b0).
    /// </summary>
    public static class VfxFrameConstants {
        /// <summary>
        /// Number of effect-defined float slots that follow the shared header.
        /// </summary>
        public const int ParamSlotCount = 16;

        /// <summary>
        /// Number of floats in the shared header: normalized time, width, height, and one pad float.
        /// </summary>
        public const int HeaderFloatCount = 4;

        /// <summary>
        /// Total float count of the constant buffer, header plus parameter slots.
        /// </summary>
        public const int TotalFloatCount = HeaderFloatCount + ParamSlotCount;

        /// <summary>
        /// Packs the per-frame header and an effect's resolved parameter slots into the flat float
        /// layout the shader cbuffer expects.
        /// </summary>
        /// <param name="normalizedTime">Clip progress in [0, 1] for the frame being rendered.</param>
        /// <param name="width">Output width in pixels.</param>
        /// <param name="height">Output height in pixels.</param>
        /// <param name="paramSlots">Effect parameter slots; must be exactly <see cref="ParamSlotCount"/> long.</param>
        /// <returns>Constant buffer contents ready to upload.</returns>
        public static float[] Build(float normalizedTime, int width, int height, float[] paramSlots) {
            if (paramSlots == null || paramSlots.Length != ParamSlotCount) {
                throw new ArgumentException($"Parameter slots must contain exactly {ParamSlotCount} values.", nameof(paramSlots));
            }

            float[] buffer = new float[TotalFloatCount];
            buffer[0] = normalizedTime;
            buffer[1] = width;
            buffer[2] = height;
            buffer[3] = 0f;
            Array.Copy(paramSlots, 0, buffer, HeaderFloatCount, ParamSlotCount);
            return buffer;
        }
    }
}
