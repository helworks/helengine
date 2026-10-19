namespace helengine.editor {
    /// <summary>
    /// Defines the shared binary container format used by tolerant editor scene component payloads.
    /// </summary>
    public static class EditorTaggedSceneComponentPayloadFormat {
        /// <summary>
        /// Current binary container version understood by the editor tagged field reader and writer.
        /// </summary>
        public const byte CurrentVersion = 2;

        /// <summary>Original named-field containers encode configurable components as fixed IEEE32 values.</summary>
        public const byte LegacyVersion = 1;

        /// <summary>Authoring retains the source value independently of any selected native cook policy.</summary>
        public static readonly NumericPrecisionPolicy AuthoringNumericPolicy = new NumericPrecisionPolicy(64, 64);

        /// <summary>Recognizes only explicitly versioned numeric field layouts.</summary>
        /// <param name="version">Stored named-field container version.</param>
        public static bool IsSupportedVersion(byte version) { return version == LegacyVersion || version == CurrentVersion; }
    }
}
