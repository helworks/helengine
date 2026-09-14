namespace helengine.editor.tests.testing {
    /// <summary>
    /// Scripted component that exposes exactly one member for every directly supported automatic-persistence leaf type so
    /// the editor write path and the runtime read path can be characterised against each other.
    /// </summary>
    public sealed class TestEveryLeafTypeSerializableComponent : Component {
        /// <summary>
        /// Gets or sets the authored string leaf value.
        /// </summary>
        public string TextValue { get; set; }

        /// <summary>
        /// Gets or sets the authored boolean leaf value.
        /// </summary>
        public bool BoolValue { get; set; }

        /// <summary>
        /// Gets or sets the authored unsigned byte leaf value.
        /// </summary>
        public byte ByteValue { get; set; }

        /// <summary>
        /// Gets or sets the authored 16-bit unsigned integer leaf value.
        /// </summary>
        public ushort UShortValue { get; set; }

        /// <summary>
        /// Gets or sets the authored 32-bit signed integer leaf value.
        /// </summary>
        public int IntValue { get; set; }

        /// <summary>
        /// Gets or sets the authored 32-bit unsigned integer leaf value.
        /// </summary>
        public uint UIntValue { get; set; }

        /// <summary>
        /// Gets or sets the authored 64-bit signed integer leaf value.
        /// </summary>
        public long LongValue { get; set; }

        /// <summary>
        /// Gets or sets the authored single-precision leaf value.
        /// </summary>
        public float FloatValue { get; set; }

        /// <summary>
        /// Gets or sets the authored double-precision leaf value.
        /// </summary>
        public double DoubleValue { get; set; }

        /// <summary>
        /// Gets or sets the authored two-component integer vector leaf value.
        /// </summary>
        public int2 Int2Value { get; set; }

        /// <summary>
        /// Gets or sets the authored four-component integer vector leaf value.
        /// </summary>
        public int4 Int4Value { get; set; }

        /// <summary>
        /// Gets or sets the authored two-component float vector leaf value.
        /// </summary>
        public float2 Float2Value { get; set; }

        /// <summary>
        /// Gets or sets the authored three-component float vector leaf value.
        /// </summary>
        public float3 Float3Value { get; set; }

        /// <summary>
        /// Gets or sets the authored four-component float vector leaf value.
        /// </summary>
        public float4 Float4Value { get; set; }

        /// <summary>
        /// Gets or sets the authored packed byte color leaf value.
        /// </summary>
        public byte4 Byte4Value { get; set; }

        /// <summary>
        /// Gets or sets the authored scene entity reference leaf value.
        /// </summary>
        public SceneEntityReference EntityReferenceValue { get; set; }
    }
}
