namespace helengine.editor {
    /// <summary>
    /// Creates a rotation gizmo composed of three hollow tube rings aligned to the world axes.
    /// </summary>
    public static class TransformRotationGizmoFactory {
        /// <summary>
        /// Inner radius of each rotation ring.
        /// </summary>
        public const float InnerRadius = 0.92f;
        /// <summary>
        /// Outer radius of each rotation ring.
        /// </summary>
        public const float OuterRadius = 1.02f;
        /// <summary>
        /// Axial thickness of each rotation ring.
        /// </summary>
        public const float RingHeight = 0.08f;
        /// <summary>
        /// Visible outer diameter of the rotation gizmo used for constant on-screen scaling.
        /// </summary>
        public const float OuterDiameter = OuterRadius * 2f;
        /// <summary>
        /// Segment count around the circular rotation ring.
        /// </summary>
        const int RingSegments = 48;
        /// <summary>
        /// Marker UV written to X-axis meshes for shader-side axis color decoding.
        /// </summary>
        static readonly float2 XAxisMarker = new float2(0.1f, 0.1f);
        /// <summary>
        /// Marker UV written to Y-axis meshes for shader-side axis color decoding.
        /// </summary>
        static readonly float2 YAxisMarker = new float2(0.9f, 0.1f);
        /// <summary>
        /// Marker UV written to Z-axis meshes for shader-side axis color decoding.
        /// </summary>
        static readonly float2 ZAxisMarker = new float2(0.1f, 0.9f);

        /// <summary>
        /// Creates a rotation gizmo using the same material for normal and highlighted states.
        /// </summary>
        /// <param name="render3D">Renderer used to build runtime mesh resources.</param>
        /// <param name="sceneCamera">Scene camera used for gizmo distance scaling.</param>
        /// <param name="gizmoMaterial">Material used by gizmo meshes.</param>
        /// <returns>Created rotation gizmo root entity.</returns>
        public static EditorEntity Create(RenderManager3D render3D, CameraComponent sceneCamera, RuntimeMaterial gizmoMaterial) {
            if (gizmoMaterial == null) {
                throw new ArgumentNullException(nameof(gizmoMaterial));
            }

            return Create(render3D, sceneCamera, gizmoMaterial, gizmoMaterial);
        }

        /// <summary>
        /// Creates a rotation gizmo root entity and registers it in the scene.
        /// </summary>
        /// <param name="render3D">Renderer used to build runtime mesh resources.</param>
        /// <param name="sceneCamera">Scene camera used for gizmo distance scaling.</param>
        /// <param name="gizmoMaterial">Base material used by gizmo meshes.</param>
        /// <param name="gizmoHighlightMaterial">Highlight material used when a ring is hovered.</param>
        /// <returns>Created rotation gizmo root entity.</returns>
        public static EditorEntity Create(
            RenderManager3D render3D,
            CameraComponent sceneCamera,
            RuntimeMaterial gizmoMaterial,
            RuntimeMaterial gizmoHighlightMaterial) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }

            if (sceneCamera == null) {
                throw new ArgumentNullException(nameof(sceneCamera));
            }

            if (gizmoMaterial == null) {
                throw new ArgumentNullException(nameof(gizmoMaterial));
            }

            if (gizmoHighlightMaterial == null) {
                throw new ArgumentNullException(nameof(gizmoHighlightMaterial));
            }

            ModelAsset xRingAsset = TransformGizmoMeshFactory.CreateTubeRing(InnerRadius, OuterRadius, RingHeight, RingSegments);
            ModelAsset yRingAsset = TransformGizmoMeshFactory.CreateTubeRing(InnerRadius, OuterRadius, RingHeight, RingSegments);
            ModelAsset zRingAsset = TransformGizmoMeshFactory.CreateTubeRing(InnerRadius, OuterRadius, RingHeight, RingSegments);
            ApplyAxisMarker(xRingAsset, XAxisMarker);
            ApplyAxisMarker(yRingAsset, YAxisMarker);
            ApplyAxisMarker(zRingAsset, ZAxisMarker);

            RuntimeModel xRingModel = render3D.BuildModelFromRaw(xRingAsset);
            RuntimeModel yRingModel = render3D.BuildModelFromRaw(yRingAsset);
            RuntimeModel zRingModel = render3D.BuildModelFromRaw(zRingAsset);

            EditorEntity gizmoRoot = new EditorEntity();
            gizmoRoot.Name = "Transform Rotation Gizmo";
            gizmoRoot.InternalEntity = true;
            gizmoRoot.LayerMask = EditorLayerMasks.SceneGizmo;
            gizmoRoot.AddComponent(new TransformRotationGizmoFollowComponent(sceneCamera, gizmoRoot, gizmoMaterial, gizmoHighlightMaterial));

            CreateRingEntity(gizmoRoot, "Transform Rotation Gizmo X", CreateXAxisOrientation(), xRingModel, gizmoMaterial);
            CreateRingEntity(gizmoRoot, "Transform Rotation Gizmo Y", float4.Identity, yRingModel, gizmoMaterial);
            CreateRingEntity(gizmoRoot, "Transform Rotation Gizmo Z", CreateZAxisOrientation(), zRingModel, gizmoMaterial);
            return gizmoRoot;
        }

        /// <summary>
        /// Writes a uniform axis marker UV to all vertices in a model asset.
        /// </summary>
        /// <param name="modelAsset">Model asset to update.</param>
        /// <param name="axisMarker">Marker UV used by shader axis-color decoding.</param>
        static void ApplyAxisMarker(ModelAsset modelAsset, float2 axisMarker) {
            if (modelAsset == null) {
                throw new ArgumentNullException(nameof(modelAsset));
            }

            if (modelAsset.TexCoords == null) {
                throw new InvalidOperationException("Model asset texture coordinates must be initialized.");
            }

            for (int texCoordIndex = 0; texCoordIndex < modelAsset.TexCoords.Length; texCoordIndex++) {
                modelAsset.TexCoords[texCoordIndex] = axisMarker;
            }
        }

        /// <summary>
        /// Creates one hollow ring entity that rotates around the axis encoded by its orientation.
        /// </summary>
        /// <param name="gizmoRoot">Root entity that owns the ring.</param>
        /// <param name="ringName">Display name for the ring entity.</param>
        /// <param name="ringOrientation">Ring orientation relative to the gizmo root.</param>
        /// <param name="ringModel">Runtime mesh used by the ring.</param>
        /// <param name="material">Material used by the ring mesh.</param>
        static void CreateRingEntity(
            EditorEntity gizmoRoot,
            string ringName,
            float4 ringOrientation,
            RuntimeModel ringModel,
            RuntimeMaterial material) {
            if (gizmoRoot == null) {
                throw new ArgumentNullException(nameof(gizmoRoot));
            }

            if (ringModel == null) {
                throw new ArgumentNullException(nameof(ringModel));
            }

            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            EditorEntity ringEntity = new EditorEntity();
            ringEntity.Name = ringName;
            ringEntity.InternalEntity = true;
            ringEntity.LayerMask = EditorLayerMasks.SceneGizmo;
            ringEntity.Enabled = false;
            ringEntity.Scale = float3.Zero;
            ringEntity.Orientation = ringOrientation;
            ringEntity.AddComponent(new TransformGizmoHandleComponent(new float3(0f, 1f, 0f)));

            MeshComponent ringMesh = new MeshComponent();
            ringMesh.Model = ringModel;
            ringMesh.Material = material;
            ringEntity.AddComponent(ringMesh);

            gizmoRoot.AddChild(ringEntity);
        }

        /// <summary>
        /// Creates the axis rotation that maps a local Y-normal ring into an X-normal ring.
        /// </summary>
        /// <returns>Quaternion rotating +Y to +X.</returns>
        static float4 CreateXAxisOrientation() {
            float3 zAxis = new float3(0f, 0f, 1f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref zAxis, (float)(-Math.PI * 0.5), out orientation);
            return orientation;
        }

        /// <summary>
        /// Creates the axis rotation that maps a local Y-normal ring into a Z-normal ring.
        /// </summary>
        /// <returns>Quaternion rotating +Y to +Z.</returns>
        static float4 CreateZAxisOrientation() {
            float3 xAxis = new float3(1f, 0f, 0f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref xAxis, (float)(Math.PI * 0.5), out orientation);
            return orientation;
        }
    }
}
