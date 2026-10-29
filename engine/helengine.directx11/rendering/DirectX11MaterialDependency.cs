namespace helengine.directx11 {
    /// <summary>Snapshots one ancestor's binding generation and controlled layout binding revisions.</summary>
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
        /// <summary>Texture collection identity retained to detect replacement.</summary>
        readonly MaterialLayoutBindings Textures;
        /// <summary>Constant collection identity retained to detect replacement.</summary>
        readonly MaterialLayoutBindings Constants;
        /// <summary>Observed texture binding mutation revision.</summary>
        readonly ulong TextureRevision;
        /// <summary>Observed constant binding mutation revision.</summary>
        readonly ulong ConstantRevision;
        /// <summary>Sampler collection identity retained to detect replacement.</summary>
        readonly MaterialLayoutBindings Samplers;
        /// <summary>Observed sampler binding mutation revision.</summary>
        readonly ulong SamplerRevision;
        /// <summary>Captures identities and revisions without scanning binding slots.</summary>
        public DirectX11MaterialDependency(ShaderRuntimeMaterial material) {
            Material = material;
            Properties = material.Properties;
            Revision = Properties.Revision;
            LayoutRevision = material.LayoutRevision;
            Parent = material.ParentMaterial;
            Textures = material.Layout.TextureBindings;
            TextureRevision = Textures.Revision;
            Constants = material.Layout.ConstantBufferBindings;
            ConstantRevision = Constants.Revision;
            Samplers = material.Layout.SamplerBindings;
            SamplerRevision = Samplers.Revision;
        }
        /// <summary>Checks collection identities and revisions in constant time without allocating on a cache hit.</summary>
        public bool IsCurrent() {
            return ReferenceEquals(Properties, Material.Properties) && Material.Properties != null
                && Revision == Material.Properties.Revision && LayoutRevision == Material.LayoutRevision
                && ReferenceEquals(Parent, Material.ParentMaterial) && Material.Layout != null
                && ReferenceEquals(Textures, Material.Layout.TextureBindings) && TextureRevision == Textures.Revision
                && ReferenceEquals(Constants, Material.Layout.ConstantBufferBindings) && ConstantRevision == Constants.Revision
                && ReferenceEquals(Samplers, Material.Layout.SamplerBindings) && SamplerRevision == Samplers.Revision;
        }
    }
}
