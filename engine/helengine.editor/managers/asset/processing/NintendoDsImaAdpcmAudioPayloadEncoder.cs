namespace helengine.editor {
    /// <summary>
    /// Encodes mono PCM16 samples into the Nintendo DS IMA ADPCM payload consumed by libnds.
    /// </summary>
    public sealed class NintendoDsImaAdpcmAudioPayloadEncoder : IEditorAudioPayloadEncoder {
        /// <summary>
        /// Stable identifier used by the Nintendo DS buffered audio profile.
        /// </summary>
        public string EncodingFamilyId => "adpcm-buffered";

        /// <summary>
        /// IMA ADPCM step-index delta table defined by the Nintendo DS decoder format.
        /// </summary>
        static readonly int[] IndexTable = [
            -1, -1, -1, -1,
             2,  4,  6,  8,
            -1, -1, -1, -1,
             2,  4,  6,  8
        ];

        /// <summary>
        /// IMA ADPCM step table defined by the Nintendo DS decoder format.
        /// </summary>
        static readonly int[] StepTable = [
                7,     8,     9,    10,    11,    12,    13,    14,
               16,    17,    19,    21,    23,    25,    28,    31,
               34,    37,    41,    45,    50,    55,    60,    66,
               73,    80,    88,    97,   107,   118,   130,   143,
              157,   173,   190,   209,   230,   253,   279,   307,
              337,   371,   408,   449,   494,   544,   598,   658,
              724,   796,   876,   963,  1060,  1166,  1282,  1411,
             1552,  1707,  1878,  2066,  2272,  2499,  2749,  3024,
             3327,  3660,  4026,  4428,  4871,  5358,  5894,  6484,
             7132,  7845,  8630,  9493, 10442, 11487, 12635, 13899,
            15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794,
            32767
        ];

        /// <summary>
        /// Encodes PCM16 samples with the four-byte predictor header followed by low-first nibbles.
        /// </summary>
        /// <param name="samples">Mono signed PCM16 samples to encode.</param>
        /// <returns>Nintendo DS IMA ADPCM payload bytes.</returns>
        public byte[] Encode(short[] samples) {
            if (samples == null) {
                throw new ArgumentNullException(nameof(samples));
            }

            if (samples.Length == 0) {
                return Array.Empty<byte>();
            }

            int nibbleCount = Math.Max(0, samples.Length - 1);
            byte[] encodedBytes = new byte[4 + ((nibbleCount + 1) / 2)];
            short predictor = samples[0];
            int stepIndex = 0;
            encodedBytes[0] = (byte)(predictor & 0xFF);
            encodedBytes[1] = (byte)((predictor >> 8) & 0xFF);
            encodedBytes[2] = (byte)stepIndex;
            encodedBytes[3] = 0;

            for (int sampleIndex = 1; sampleIndex < samples.Length; sampleIndex++) {
                byte nibble = EncodeNibble(samples[sampleIndex], ref predictor, ref stepIndex);
                int payloadByteIndex = 4 + ((sampleIndex - 1) / 2);
                if (((sampleIndex - 1) & 1) == 0) {
                    encodedBytes[payloadByteIndex] = nibble;
                } else {
                    encodedBytes[payloadByteIndex] |= (byte)(nibble << 4);
                }
            }

            return encodedBytes;
        }

        /// <summary>
        /// Encodes one sample into one IMA ADPCM nibble while updating decoder state.
        /// </summary>
        /// <param name="sample">Sample to approximate.</param>
        /// <param name="predictor">Current predictor, updated in place.</param>
        /// <param name="stepIndex">Current step index, updated in place.</param>
        /// <returns>Four-bit ADPCM nibble.</returns>
        static byte EncodeNibble(short sample, ref short predictor, ref int stepIndex) {
            int step = StepTable[stepIndex];
            int delta = sample - predictor;
            int nibble = 0;
            if (delta < 0) {
                nibble = 8;
                delta = -delta;
            }

            int diff = step >> 3;
            if (delta >= step) {
                nibble |= 4;
                delta -= step;
                diff += step;
            }

            step >>= 1;
            if (delta >= step) {
                nibble |= 2;
                delta -= step;
                diff += step;
            }

            step >>= 1;
            if (delta >= step) {
                nibble |= 1;
                diff += step;
            }

            int predictorValue = predictor;
            predictor = ClampToInt16(predictorValue + ((nibble & 8) != 0 ? -diff : diff));
            stepIndex = Math.Clamp(stepIndex + IndexTable[nibble], 0, StepTable.Length - 1);
            return (byte)nibble;
        }

        /// <summary>
        /// Clamps an intermediate predictor value into the signed PCM16 range.
        /// </summary>
        /// <param name="value">Intermediate predictor value.</param>
        /// <returns>Clamped signed sample.</returns>
        static short ClampToInt16(int value) {
            return (short)Math.Clamp(value, short.MinValue, short.MaxValue);
        }
    }
}
