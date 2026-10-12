namespace helengine.editor {
    /// <summary>Combines render-model caps with an explicit generated-sphere tessellation choice.</summary>
    public sealed class ModelGpuReductionProfile {
        /// <summary>Reduction limits applied only to models consumed by MeshComponent rendering.</summary>
        public ModelGpuReductionSettings Reduction { get; set; } = new ModelGpuReductionSettings();
        /// <summary>Latitude segments used by the independent GPU generated-sphere variant.</summary>
        public int SphereRings { get; set; } = 6;
        /// <summary>Longitude segments used by the independent GPU generated-sphere variant.</summary>
        public int SphereSectors { get; set; } = 12;
        /// <summary>Clones inherited settings so one component cannot modify another component's profile.</summary>
        public ModelGpuReductionProfile Clone() {
            return new ModelGpuReductionProfile { Reduction = Reduction.Clone(), SphereRings = SphereRings, SphereSectors = SphereSectors };
        }
        /// <summary>Checks finite discrete settings and exact axis extrema for the generated sphere.</summary>
        public void Validate() {
            if (Reduction == null) throw new InvalidOperationException("GPU reduction profile requires its limits.");
            Reduction.Validate();
            if (SphereRings < 2 || SphereRings % 2 != 0 || SphereSectors < 4 || SphereSectors % 4 != 0 || (long)(SphereRings + 1) * (SphereSectors + 1) > ushort.MaxValue) throw new InvalidOperationException("GPU sphere rings must be positive/even and sectors divisible by four, within the 16-bit vertex range.");
        }
    }
}
