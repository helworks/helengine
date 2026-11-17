namespace helengine.ui.format {
    /// <summary>Admission limits shared by the writer, reader and generated TypeScript.</summary>
    public static class UiFormatLimits {
        /// <summary>Maximum complete document length, including the header.</summary>
        public const int Bytes = 4194304;
        /// <summary>Maximum encoded UTF-8 bytes in any single string.</summary>
        public const int StringBytes = 4096;
        /// <summary>Maximum named metrics in a document.</summary>
        public const int Metrics = 256;
        /// <summary>Maximum templates in a document.</summary>
        public const int Templates = 64;
        /// <summary>Maximum nodes across all templates, not per template.</summary>
        public const int Nodes = 4096;
        /// <summary>Maximum components on a node.</summary>
        public const int Components = 5;
        /// <summary>Maximum properties on a component.</summary>
        public const int Properties = 19;
    }
}
