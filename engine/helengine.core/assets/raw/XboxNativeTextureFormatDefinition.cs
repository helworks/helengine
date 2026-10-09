namespace helengine {
    /// <summary>Describes one Original Xbox hardware color code, storage layout and its representable alpha policies.</summary>
    public sealed class XboxNativeTextureFormatDefinition : IDisposable {
        /// <summary>Owns the representable alpha policies for this description until it is disposed.</summary>
        [NativeOwnedMember]
        TextureAssetAlphaPrecision[] AlphaPrecisions;

        /// <summary>Initializes the immutable description used by cookers and the native payload codec.</summary>
        public XboxNativeTextureFormatDefinition(string id, int hardwareFormat, int bytesPerTexel, bool isLinear,
            TextureAssetAlphaPrecision maximumAlphaPrecision, bool hasCoupledAlpha) {
            Id = id;
            DisplayName = id.Replace("Xbox.", "Xbox ").Replace(".", " ");
            HardwareFormat = hardwareFormat;
            BytesPerTexel = bytesPerTexel;
            IsLinear = isLinear;
            IsCompressed = hardwareFormat == 0x0c || hardwareFormat == 0x0e || hardwareFormat == 0x0f;
            IsPaletted = hardwareFormat == 0x0b;
            IsYuv = hardwareFormat == 0x24 || hardwareFormat == 0x25;
            HasCoupledAlpha = hasCoupledAlpha;
            DefaultAlphaPrecision = maximumAlphaPrecision;
            if (hasCoupledAlpha) {
                AlphaPrecisions = new TextureAssetAlphaPrecision[] { maximumAlphaPrecision };
            } else {
                AlphaPrecisions = new TextureAssetAlphaPrecision[(int)maximumAlphaPrecision + 1];
                for (int index = 0; index < AlphaPrecisions.Length; index++) {
                    AlphaPrecisions[index] = (TextureAssetAlphaPrecision)index;
                }
            }
        }

        /// <summary>Gets the stable platform selection identifier, independent of the generic color-format enum.</summary>
        public string Id { get; }
        /// <summary>Gets the human-readable layout and format name.</summary>
        public string DisplayName { get; }
        /// <summary>Gets the unshifted NV097 color code.</summary>
        public int HardwareFormat { get; }
        /// <summary>Gets storage bytes per uncompressed texel; compressed formats use block sizes instead.</summary>
        public int BytesPerTexel { get; }
        /// <summary>Gets whether the payload stores byte-pitched rows rather than Morton-addressed texels.</summary>
        public bool IsLinear { get; }
        /// <summary>Gets whether the payload stores row-major DXT blocks.</summary>
        public bool IsCompressed { get; }
        /// <summary>Gets whether texels are eight-bit indices into an ARGB8888 palette.</summary>
        public bool IsPaletted { get; }
        /// <summary>Gets whether the payload stores packed YUV422 horizontal pairs.</summary>
        public bool IsYuv { get; }
        /// <summary>Gets whether the sampled alpha is inseparable from intensity or a stored color component.</summary>
        public bool HasCoupledAlpha { get; }
        /// <summary>Gets the preferred intrinsic alpha policy for a new selection.</summary>
        public TextureAssetAlphaPrecision DefaultAlphaPrecision { get; }
        /// <summary>Gets the catalog-owned policies; callers must retain these values without changing the array.</summary>
        [NativeBorrowedReturn]
        public TextureAssetAlphaPrecision[] SupportedAlphaPrecisions { get { return AlphaPrecisions; } }

        /// <summary>Releases the description's policy array; permanent catalog descriptions remain borrowed for the process lifetime.</summary>
        public void Dispose() {
            NativeOwnership.Release(ref AlphaPrecisions);
        }

        /// <summary>Reports whether a requested precision can be represented without changing intrinsic sampling semantics.</summary>
        public bool SupportsAlpha(TextureAssetAlphaPrecision alphaPrecision) {
            if (AlphaPrecisions == null) return false;
            for (int index = 0; index < AlphaPrecisions.Length; index++) {
                if (AlphaPrecisions[index] == alphaPrecision) return true;
            }
            return false;
        }
    }
}
