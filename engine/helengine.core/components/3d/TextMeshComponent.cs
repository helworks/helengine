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
        /// <summary>Borrows the optional material template used to instantiate portable per-page text materials.</summary>
        RuntimeMaterial AtlasMaterialValue;
        /// <summary>Owns the currently visible page pins and material instances.</summary>
        [NativeOwnedMember] TextMeshPageBindings PageBindings;
        /// <summary>Owns the atlas-run indices corresponding to the current generated geometry.</summary>
        [NativeOwnedMember] int[] GeometryPages;
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
            set { ThrowIfDisposed(); if (!ReferenceEquals(FontValue, value)) { ReleasePageBindings(); FontValue = value; GeometryDirty = true; } }
        }
        /// <summary>Gets or sets the full text; changing it invalidates only this component's generated geometry.</summary>
        public string Text {
            get { return TextValue; }
            set {
                ThrowIfDisposed();
                if (value == null) { throw new ArgumentNullException(nameof(value)); }
                if (TextValue != value) { ReleasePageBindings(); TextValue = value; GeometryDirty = true; }
            }
        }
        /// <summary>Gets or sets a borrowed material template; visible text owns independent instances for all used font pages.</summary>
        /// <remarks>Leave null to preserve the legacy single-atlas Materials contract. Changing this template does not rebuild geometry.</remarks>
        public RuntimeMaterial AtlasMaterial {
            get { return AtlasMaterialValue; }
            set { ThrowIfDisposed(); if (!ReferenceEquals(AtlasMaterialValue, value)) { ReleasePageBindings(); AtlasMaterialValue = value; } }
        }
        /// <summary>Gets visible generated page slots when AtlasMaterial is set, or gets and replaces borrowed legacy atlas slots otherwise.</summary>
        public RuntimeMaterial[] Materials {
            get {
                ThrowIfDisposed();
                if (AtlasMaterialValue != null && Parent != null && Parent.IsHierarchyEnabled) { RuntimeModel model = Model; }
                return PageBindings == null ? MaterialSlots : PageBindings.Materials;
            }
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
                if (Parent == null || !Parent.IsHierarchyEnabled) { return null; }
                if (TextValue.Length > 0) { ValidateAtlasMaterial(); }
                if (GeometryDirty) { RebuildGeometry(); }
                if (AtlasMaterialValue != null && GeneratedModel != null && PageBindings == null) {
                    NativeOwnership.DisposeAndRelease(ref PageBindings);
                    PageBindings = TextMeshPageBindings.Create(FontValue, AtlasMaterialValue, GeometryPages);
                }
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
        public void InvalidateGeometry() { ThrowIfDisposed(); ReleasePageBindings(); GeometryDirty = true; }
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
            if (newEnabled && !IsDisposed) { OwnerCore.ObjectManager.RegisterForRender3D(this); } else { OwnerCore.ObjectManager.RemoveFromRender3D(this); ReleasePageBindings(); }
        }
        /// <summary>Releases only generated model and slot storage, leaving borrowed font, atlas and materials under their existing owners.</summary>
        public override void Dispose() {
            if (Parent != null) { OwnerCore.ObjectManager.RemoveFromRender3D(this); }
            NativeOwnership.DisposeAndRelease(ref PageBindings);
            ReleaseGeometry(); NativeOwnership.Release(ref GeometryPages); NativeOwnership.Release(ref MaterialSlots); FontValue = null; AtlasMaterialValue = null;
            base.Dispose();
        }
        /// <summary>Requires one explicit live atlas binding so text cannot silently draw with an unrelated or uninitialized material.</summary>
        void ValidateAtlasMaterial() {
            if (FontValue == null || FontValue.IsDisposed || FontValue.Texture == null || FontValue.Texture.IsDisposed) {
                throw new InvalidOperationException("3D text requires a live engine font and its atlas texture.");
            }
            if (AtlasMaterialValue != null) { return; }
            if (MaterialSlots.Length != 1 || MaterialSlots[0] == null || !ReferenceEquals(MaterialSlots[0].ResolvePrimaryTexture(), FontValue.Texture)) {
                throw new InvalidOperationException("3D text requires one caller-authored material bound to its font atlas.");
            }
        }
        /// <summary>Builds replacement geometry completely before releasing the previous valid model; failures preserve ownership and remain visible.</summary>
        void RebuildGeometry() {
            RuntimeModel replacement = null; RenderManager3D renderer = null; int[] pages = null;
            if (TextValue.Length > 0) {
                using (ModelAsset geometry = TextMeshGeometry.Build(FontValue, TextValue)) {
                    if (geometry.Positions.Length > 0) {
                        renderer = OwnerCore.RenderManager3D;
                        if (renderer == null) { throw new InvalidOperationException("3D text requires the owning core's 3D renderer."); }
                        pages = TextMeshGeometry.GetPageIndices(FontValue, TextValue);
                        if (AtlasMaterialValue == null) {
                            for (int index = 0; index < pages.Length; index++) {
                                if (pages[index] != 0) { NativeOwnership.Release(ref pages); throw new InvalidOperationException("Paged 3D text requires an AtlasMaterial template."); }
                            }
                        }
                        try { replacement = renderer.BuildModelFromRaw(geometry); } catch { NativeOwnership.Release(ref pages); throw; }
                        if (replacement == null) { NativeOwnership.Release(ref pages); throw new InvalidOperationException("The 3D renderer returned no model for text geometry."); }
                    }
                }
            }
            ReleaseGeometry(); GeneratedModel = replacement; ModelRenderer = renderer; NativeOwnership.Release(ref GeometryPages); GeometryPages = pages; GeometryDirty = false;
        }
        /// <summary>Releases visible atlas pins and owned material instances without rebuilding stable geometry.</summary>
        void ReleasePageBindings() { NativeOwnership.DisposeAndRelease(ref PageBindings); }
        /// <summary>Clears the owned model before releasing it once through the renderer that created it.</summary>
        void ReleaseGeometry() {
            RuntimeModel model = GeneratedModel; RenderManager3D renderer = ModelRenderer;
            ReleasePageBindings(); NativeOwnership.Release(ref GeometryPages);
            GeneratedModel = null; ModelRenderer = null;
            if (model != null) { renderer.ReleaseModel(model); }
        }
    }
}