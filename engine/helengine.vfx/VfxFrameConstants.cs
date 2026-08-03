namespace helengine.vfx {
    /// <summary>
    /// Describes the fixed constant-buffer layout every VFX effect shader shares: normalized time,
    /// resolution, and a fixed bank of parameter slots. Must stay in sync with each effect's HLSL
    /// cbuffer declaration (register b0).
    /// </summary>
    public static class VfxFrameConstants {
        public const int ParamSlotCount = 16;
        public const int HeaderFloatCount = 4;
        public const int TotalFloatCount = HeaderFloatCount + ParamSlotCount;

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
