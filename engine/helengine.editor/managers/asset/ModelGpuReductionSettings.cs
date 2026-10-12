namespace helengine.editor {
    /// <summary>Limits a separate render-model variant without changing imported or CPU-readable geometry.</summary>
    public sealed class ModelGpuReductionSettings {
        /// <summary>Enables deterministic render-only reduction for the selected scope.</summary>
        public bool Enabled { get; set; }
        /// <summary>Maximum number of distinct attribute vertices in the final render variant.</summary>
        public int MaximumVertices { get; set; } = 256;
        /// <summary>Maximum number of triangle indices in the final render variant.</summary>
        public int MaximumIndices { get; set; } = 768;
        /// <summary>Creates an independent settings value for inherited scopes and build snapshots.</summary>
        public ModelGpuReductionSettings Clone() {
            return new ModelGpuReductionSettings { Enabled = Enabled, MaximumVertices = MaximumVertices, MaximumIndices = MaximumIndices };
        }
        /// <summary>Rejects limits that cannot describe a nonempty indexed triangle mesh.</summary>
        public void Validate() {
            if (MaximumVertices < 3 || MaximumVertices > ushort.MaxValue || MaximumIndices < 3 || MaximumIndices % 3 != 0) {
                throw new InvalidOperationException("GPU model limits require 3..65535 vertices and a positive triangle-aligned index count.");
            }
        }
    }
}
