namespace helengine {
    /// <summary>Draws text from the engine's own font atlas as ordinary planar 3D model geometry.</summary>
    [RunInEditor]
    public sealed class TextMeshComponent : Component, IDrawable3D, IPlanarDrawable3D {
        /// <summary>Borrows the explicitly generated font and its atlas without assuming asset ownership.</summary>
        FontAsset FontValue;
        /// <summary>Stores the complete requested text without replacing missing glyphs.</summary>
        string TextValue = string.Empty;
        /// <summary>Owns the material-slot container while borrowing each caller-authored material.</summary>
        [NativeOwnedMember]
        RuntimeMaterial[] MaterialSlots = new RuntimeMaterial[0];
        /// <summary>Owns the model created from the latest text geometry until replacement or detach.</summary>
        RuntimeModel GeneratedModel;
        /// <summary>Remembers the exact renderer that must release the owned model, independent of subsequent entity attachment.</summary>
        RenderManager3D ModelRenderer;
        /// <summary>Marks content or metric changes without rebuilding for entity transforms.</summary>
        bool GeometryDirty = true;
        /// <summary>Stores the ordering key registered with the 3D object manager.</summary>
        byte RenderOrderValue;

        /// <summary>Gets or sets the borrowed engine font; its explicit atlas must cover every non-control text character.</summary>
        public FontAsset Font {
            get { return FontValue; }
            set { ThrowIfDisposed(); if (!ReferenceEquals(FontValue, value)) { FontValue = value; GeometryDirty = true; } }
        }
        /// <summary>Gets or sets the full text; changing it invalidates only this component's generated geometry.</summary>
        public string Text {
            get { return TextValue; }
            set {
                ThrowIfDisposed();
                if (value == null) { throw new ArgumentNullException(nameof(value)); }
                if (TextValue != value) { TextValue = value; GeometryDirty = true; }
            }
        }
        /// <summary>Gets or replaces the borrowed atlas material slot, copying the supplied container without mutating shared materials.</summary>
        public RuntimeMaterial[] Materials {
            get { return MaterialSlots; }
            set {
                ThrowIfDisposed();
                if (value == null) { throw new ArgumentNullException(nameof(value)); }
                RuntimeMaterial[] slots = new RuntimeMaterial[value.Length]; Array.Copy(value, slots, value.Length);
                NativeOwnership.Release(ref MaterialSlots); MaterialSlots = slots;
            }
        }
        /// <summary>Gets or changes this drawable's ordering key through normal object-manager registration.</summary>
        public byte RenderOrder3D {
            get { return RenderOrderValue; }
            set {
                ThrowIfDisposed();
                if (RenderOrderValue == value) { return; }
                bool registered = Parent != null && Parent.IsHierarchyEnabled;
                if (registered) { OwnerCore.ObjectManager.RemoveFromRender3D(this); }
                RenderOrderValue = value;
                if (registered) { OwnerCore.ObjectManager.RegisterForRender3D(this); }
            }
        }
        /// <summary>Gets the owned runtime-only glyph model, rebuilding once after content changes and never uploading empty runs.</summary>
        [ScenePersistenceIgnore]
        public RuntimeModel Model {
            get {
                ThrowIfDisposed();
                if (Parent == null) { return null; }
                if (TextValue.Length > 0) { ValidateAtlasMaterial(); }
                if (GeometryDirty) { RebuildGeometry(); }
                return GeneratedModel;
            }
        }
        /// <summary>Gets the plane normal from the complete affine hierarchy; collapsed planes expose zero for depth-order fallback.</summary>
        [ScenePersistenceIgnore]
        public float3 WorldPlaneNormal {
            get {
                if (Parent == null) { return float3.UnitZ; }
                float4x4 world = Parent.WorldTransformMatrix;
                float3 tangentX = new float3(world.M11, world.M12, world.M13);
                float3 tangentY = new float3(world.M21, world.M22, world.M23);
                float3 normal = float3.Cross(tangentX, tangentY);
                return normal.LengthSquared() > 0 ? float3.Normalize(normal) : float3.Zero;
            }
        }

        /// <summary>Marks external changes to borrowed font metrics for one rebuild on the next geometry request.</summary>
        public void InvalidateGeometry() { ThrowIfDisposed(); GeometryDirty = true; }
        /// <summary>Registers this drawable without creating graphics resources during scene attachment.</summary>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            GeometryDirty = true;
            if (entity.IsHierarchyEnabled) { OwnerCore.ObjectManager.RegisterForRender3D(this); }
        }
        /// <summary>Unregisters and releases generated graphics through their creating renderer before the entity clears ownership.</summary>
        public override void ComponentRemoved(Entity entity) {
            OwnerCore.ObjectManager.RemoveFromRender3D(this); ReleaseGeometry(); GeometryDirty = true;
            base.ComponentRemoved(entity);
        }
        /// <summary>Mirrors ordinary mesh registration as the owning hierarchy becomes enabled or disabled.</summary>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);
            if (newEnabled && !IsDisposed) { OwnerCore.ObjectManager.RegisterForRender3D(this); } else { OwnerCore.ObjectManager.RemoveFromRender3D(this); }
        }
        /// <summary>Releases only generated model and slot storage, leaving borrowed font, atlas and materials under their existing owners.</summary>
        public override void Dispose() {
            if (Parent != null) { OwnerCore.ObjectManager.RemoveFromRender3D(this); }
            ReleaseGeometry(); NativeOwnership.Release(ref MaterialSlots); FontValue = null;
            base.Dispose();
        }
        /// <summary>Requires one explicit live atlas binding so text cannot silently draw with an unrelated or uninitialized material.</summary>
        void ValidateAtlasMaterial() {
            if (FontValue == null || FontValue.IsDisposed || FontValue.Texture == null || FontValue.Texture.IsDisposed) {
                throw new InvalidOperationException("3D text requires a live engine font and its atlas texture.");
            }
            if (MaterialSlots.Length != 1 || MaterialSlots[0] == null || !ReferenceEquals(MaterialSlots[0].ResolvePrimaryTexture(), FontValue.Texture)) {
                throw new InvalidOperationException("3D text requires one caller-authored material bound to its font atlas.");
            }
        }
        /// <summary>Builds replacement geometry completely before releasing the previous valid model; failures preserve ownership and remain visible.</summary>
        void RebuildGeometry() {
            RuntimeModel replacement = null; RenderManager3D renderer = null;
            if (TextValue.Length > 0) {
                using (ModelAsset geometry = TextMeshGeometry.Build(FontValue, TextValue)) {
                    if (geometry.Positions.Length > 0) {
                        renderer = OwnerCore.RenderManager3D;
                        if (renderer == null) { throw new InvalidOperationException("3D text requires the owning core's 3D renderer."); }
                        replacement = renderer.BuildModelFromRaw(geometry);
                        if (replacement == null) { throw new InvalidOperationException("The 3D renderer returned no model for text geometry."); }
                    }
                }
            }
            ReleaseGeometry(); GeneratedModel = replacement; ModelRenderer = renderer; GeometryDirty = false;
        }
        /// <summary>Clears the owned model before releasing it once through the renderer that created it.</summary>
        void ReleaseGeometry() {
            RuntimeModel model = GeneratedModel; RenderManager3D renderer = ModelRenderer;
            GeneratedModel = null; ModelRenderer = null;
            if (model != null) { renderer.ReleaseModel(model); }
        }
    }
}