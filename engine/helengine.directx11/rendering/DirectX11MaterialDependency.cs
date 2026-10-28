namespace helengine.directx11 {
    /// <summary>Snapshots one ancestor's binding generation and exact mutable layout contents.</summary>
    sealed class DirectX11MaterialDependency {
        /// <summary>Material whose changes invalidate prepared descendants.</summary>
        public ShaderRuntimeMaterial Material { get; }
        /// <summary>Property-block identity retained to detect replacement.</summary>
        readonly MaterialPropertyBlock Properties;
        /// <summary>Property mutation revision observed during preparation.</summary>
        readonly ulong Revision;
        /// <summary>Layout replacement generation observed during preparation.</summary>
        readonly ulong LayoutRevision;
        /// <summary>Parent identity observed during preparation.</summary>
        readonly RuntimeMaterial Parent;
        /// <summary>Immutable binding object identities copied from the mutable texture array.</summary>
        readonly MaterialLayoutBinding[] Textures;
        /// <summary>Immutable binding object identities copied from the mutable constant array.</summary>
        readonly MaterialLayoutBinding[] Constants;
        /// <summary>Captures state without retaining live mutable binding arrays.</summary>
        public DirectX11MaterialDependency(ShaderRuntimeMaterial material) {
            Material = material;
            Properties = material.Properties;
            Revision = Properties.Revision;
            LayoutRevision = material.LayoutRevision;
            Parent = material.ParentMaterial;
            Textures = (MaterialLayoutBinding[])material.Layout.TextureBindings.Clone();
            Constants = (MaterialLayoutBinding[])material.Layout.ConstantBufferBindings.Clone();
        }
        /// <summary>Checks revisions and every exposed binding element without allocating on a cache hit.</summary>
        public bool IsCurrent() {
            return ReferenceEquals(Properties, Material.Properties) && Material.Properties != null
                && Revision == Material.Properties.Revision && LayoutRevision == Material.LayoutRevision
                && ReferenceEquals(Parent, Material.ParentMaterial) && Material.Layout != null
                && Matches(Textures, Material.Layout.TextureBindings) && Matches(Constants, Material.Layout.ConstantBufferBindings);
        }
        /// <summary>Compares exact immutable binding identities, detecting in-place array replacement without hash collisions.</summary>
        static bool Matches(MaterialLayoutBinding[] expected, MaterialLayoutBinding[] actual) {
            if (actual == null || expected.Length != actual.Length) {
                return false;
            }
            for (int i = 0; i < expected.Length; i++) {
                if (!ReferenceEquals(expected[i], actual[i])) {
                    return false;
                }
            }
            return true;
        }
    }
}
