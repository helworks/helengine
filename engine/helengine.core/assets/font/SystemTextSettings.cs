namespace helengine {
    /// <summary>Persistence snapshot for immutable runtime typography, using the existing nested scene-object contract.</summary>
    public sealed class SystemTextSettings {
        /// <summary>Creates a writable body-style snapshot for the scene deserializer.</summary>
        public SystemTextSettings() {
            Role = SystemTextRole.Body;
        }
        /// <summary>Gets or sets the persisted semantic preferred-size role.</summary>
        public SystemTextRole Role { get; set; }
        /// <summary>Gets or sets the persisted system-font weight.</summary>
        public SystemTextWeight Weight { get; set; }
        /// <summary>Gets or sets the persisted rounded system-design request.</summary>
        public bool Rounded { get; set; }
        /// <summary>Gets or sets the base reference-canvas size, with zero selecting the semantic default.</summary>
        public float PointSize { get; set; }
    }
}
