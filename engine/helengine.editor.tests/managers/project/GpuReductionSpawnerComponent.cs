namespace helengine.editor.tests.managers.project {
    /// <summary>Represents typed render models loaded by gameplay rather than a persisted MeshComponent.</summary>
    public sealed class GpuReductionSpawnerComponent : Component {
        /// <summary>Generated model later used by the spawner's renderer.</summary>
        public RuntimeModel GeneratedModel { get; set; }
        /// <summary>Imported model later used by the spawner's renderer.</summary>
        public RuntimeModel ImportedModel { get; set; }
    }
}
