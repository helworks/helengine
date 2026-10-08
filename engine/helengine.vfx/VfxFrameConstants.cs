namespace helengine.vfx {
    /// <summary>
    /// Builds the per-pass constant buffer payload every effect shader receives at register b0. The float layout must
    /// match the <c>VfxFrameConstants</c> cbuffer in <c>VfxCommon.hlsli</c> exactly.
    /// </summary>
    public static class VfxFrameConstants {
        /// <summary>
        /// Number of effect parameter floats, packed into Params0..Params3.
        /// </summary>
        public const int ParamSlotCount = 16;

        /// <summary>
        /// Number of header floats: normalized time, pass target width and height, and one reserved float.
        /// </summary>
        public const int HeaderFloatCount = 4;

        /// <summary>
        /// Number of trailing floats: the pass constants, the pass target texel size and the main input texel size.
        /// </summary>
        public const int PassFloatCount = 8;

        /// <summary>
        /// Total float count of the buffer; a multiple of four so the buffer stays 16-byte aligned.
        /// </summary>
        public const int TotalFloatCount = HeaderFloatCount + ParamSlotCount + PassFloatCount;

        /// <summary>
        /// Builds the payload for a single-pass effect drawing at the size of its main input.
        /// </summary>
        /// <param name="normalizedTime">Clip progress in [0, 1].</param>
        /// <param name="width">Output width in pixels.</param>
        /// <param name="height">Output height in pixels.</param>
        /// <param name="paramSlots">Exactly <see cref="ParamSlotCount"/> resolved parameter values.</param>
        /// <returns>Constant buffer floats in shader layout order.</returns>
        public static float[] Build(float normalizedTime, int width, int height, float[] paramSlots) {
            return Build(normalizedTime, width, height, paramSlots, new float4(0, 0, 0, 0), width, height);
        }

        /// <summary>
        /// Builds the payload for one pass of a multi-pass effect.
        /// </summary>
        /// <param name="normalizedTime">Clip progress in [0, 1].</param>
        /// <param name="targetWidth">Width of the target this pass renders into.</param>
        /// <param name="targetHeight">Height of the target this pass renders into.</param>
        /// <param name="paramSlots">Exactly <see cref="ParamSlotCount"/> resolved parameter values.</param>
        /// <param name="passConstants">Constants fixed by the pass definition.</param>
        /// <param name="mainWidth">Width of the effect's main input.</param>
        /// <param name="mainHeight">Height of the effect's main input.</param>
        /// <returns>Constant buffer floats in shader layout order.</returns>
        public static float[] Build(float normalizedTime, int targetWidth, int targetHeight, float[] paramSlots, float4 passConstants, int mainWidth, int mainHeight) {
            if (paramSlots == null || paramSlots.Length != ParamSlotCount) {
                throw new ArgumentException($"Parameter slots must contain exactly {ParamSlotCount} values.", nameof(paramSlots));
            }
            if (targetWidth <= 0 || targetHeight <= 0 || mainWidth <= 0 || mainHeight <= 0) {
                throw new ArgumentException("Pass and main input sizes must be positive.");
            }

            float[] buffer = new float[TotalFloatCount];
            buffer[0] = normalizedTime;
            buffer[1] = targetWidth;
            buffer[2] = targetHeight;
            buffer[3] = 0f;
            Array.Copy(paramSlots, 0, buffer, HeaderFloatCount, ParamSlotCount);
            int pass = HeaderFloatCount + ParamSlotCount;
            buffer[pass] = passConstants.X;
            buffer[pass + 1] = passConstants.Y;
            buffer[pass + 2] = passConstants.Z;
            buffer[pass + 3] = passConstants.W;
            buffer[pass + 4] = 1f / targetWidth;
            buffer[pass + 5] = 1f / targetHeight;
            buffer[pass + 6] = 1f / mainWidth;
            buffer[pass + 7] = 1f / mainHeight;
            return buffer;
        }
    }
}
