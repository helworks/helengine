namespace helengine.editor {
    /// <summary>
    /// Creates translation gizmo entities composed of shaft and cone meshes for each axis.
    /// </summary>
    public static class TransformTranslationGizmoFactory {
        /// <summary>
        /// Radius for axis shaft cylinders.
        /// </summary>
        public const float ShaftRadius = 0.04f;
        /// <summary>
        /// Length for axis shaft cylinders.
        /// </summary>
        public const float ShaftLength = 0.9f;
        /// <summary>
        /// Radius for axis cone tips.
        /// </summary>
        public const float TipRadius = 0.1f;
        /// <summary>
        /// Length for axis cone tips.
        /// </summary>
        public const float TipLength = 0.25f;
        /// <summary>
        /// Total axis length from origin to cone tip used for view-percentage scaling.
        /// </summary>
        public const float AxisLength = ShaftLength + TipLength;
        /// <summary>
        /// Side length of planar translation handles.
        /// </summary>
        public const float PlaneSize = 0.25f;
        /// <summary>
        /// Offset from the origin where planar translation handles begin.
        /// </summary>
        public const float PlaneInset = 0.20f;
        /// <summary>
        /// Segment count used by generated cylinders and cones.
        /// </summary>
        const int AxisSegments = 18;
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
        /// Marker UV written to XY-plane handle meshes for shader-side color decoding.
        /// </summary>
        static readonly float2 XyPlaneMarker = new float2(0.9f, 0.9f);
        /// <summary>
        /// Marker UV written to XZ-plane handle meshes for shader-side color decoding.
        /// </summary>
        static readonly float2 XzPlaneMarker = new float2(0.5f, 0.9f);
        /// <summary>
        /// Marker UV written to YZ-plane handle meshes for shader-side color decoding.
        /// </summary>
        static readonly float2 YzPlaneMarker = new float2(0.9f, 0.5f);
        /// <summary>
        /// Creates a translation gizmo using the same material for normal and highlighted states.
        /// </summary>
        /// <param name="render3D">Renderer used to build runtime mesh resources.</param>
        /// <param name="sceneCamera">Scene camera used for gizmo distance scaling.</param>
        /// <param name="gizmoMaterial">Material used by gizmo meshes.</param>
        /// <returns>Created gizmo root entity.</returns>
        public static EditorEntity Create(RenderManager3D render3D, CameraComponent sceneCamera, RuntimeMaterial gizmoMaterial) {
            if (gizmoMaterial == null) {
                throw new ArgumentNullException(nameof(gizmoMaterial));
            }

            return Create(render3D, sceneCamera, gizmoMaterial, gizmoMaterial);
        }

        /// <summary>
        /// Creates a translation gizmo root entity and registers it in the scene.
        /// </summary>
        /// <param name="render3D">Renderer used to build runtime mesh resources.</param>
        /// <param name="sceneCamera">Scene camera used for gizmo distance scaling.</param>
        /// <param name="gizmoMaterial">Base material used by gizmo meshes.</param>
        /// <param name="gizmoHighlightMaterial">Highlight material used when an axis is hovered.</param>
        /// <returns>Created gizmo root entity.</returns>
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

            ModelAsset xShaftAsset = TransformGizmoMeshFactory.CreateCylinder(ShaftRadius, ShaftLength, AxisSegments);
            ModelAsset xTipAsset = TransformGizmoMeshFactory.CreateCone(TipRadius, TipLength, AxisSegments);
            ModelAsset yShaftAsset = TransformGizmoMeshFactory.CreateCylinder(ShaftRadius, ShaftLength, AxisSegments);
            ModelAsset yTipAsset = TransformGizmoMeshFactory.CreateCone(TipRadius, TipLength, AxisSegments);
            ModelAsset zShaftAsset = TransformGizmoMeshFactory.CreateCylinder(ShaftRadius, ShaftLength, AxisSegments);
            ModelAsset zTipAsset = TransformGizmoMeshFactory.CreateCone(TipRadius, TipLength, AxisSegments);
            ModelAsset xyPlaneAsset = TransformGizmoMeshFactory.CreatePlaneSquare(PlaneSize);
            ModelAsset xzPlaneAsset = TransformGizmoMeshFactory.CreatePlaneSquare(PlaneSize);
            ModelAsset yzPlaneAsset = TransformGizmoMeshFactory.CreatePlaneSquare(PlaneSize);

            ApplyAxisMarker(xShaftAsset, XAxisMarker);
            ApplyAxisMarker(xTipAsset, XAxisMarker);
            ApplyAxisMarker(yShaftAsset, YAxisMarker);
            ApplyAxisMarker(yTipAsset, YAxisMarker);
            ApplyAxisMarker(zShaftAsset, ZAxisMarker);
            ApplyAxisMarker(zTipAsset, ZAxisMarker);
            ApplyAxisMarker(xyPlaneAsset, XyPlaneMarker);
            ApplyAxisMarker(xzPlaneAsset, XzPlaneMarker);
            ApplyAxisMarker(yzPlaneAsset, YzPlaneMarker);

            RuntimeModel xShaftModel = render3D.BuildModelFromRaw(xShaftAsset);
            RuntimeModel xTipModel = render3D.BuildModelFromRaw(xTipAsset);
            RuntimeModel yShaftModel = render3D.BuildModelFromRaw(yShaftAsset);
            RuntimeModel yTipModel = render3D.BuildModelFromRaw(yTipAsset);
            RuntimeModel zShaftModel = render3D.BuildModelFromRaw(zShaftAsset);
            RuntimeModel zTipModel = render3D.BuildModelFromRaw(zTipAsset);
            RuntimeModel xyPlaneModel = render3D.BuildModelFromRaw(xyPlaneAsset);
            RuntimeModel xzPlaneModel = render3D.BuildModelFromRaw(xzPlaneAsset);
            RuntimeModel yzPlaneModel = render3D.BuildModelFromRaw(yzPlaneAsset);

            EditorEntity gizmoRoot = new EditorEntity();
            gizmoRoot.Name = "Transform Translation Gizmo";
            gizmoRoot.InternalEntity = true;
            gizmoRoot.LayerMask = EditorLayerMasks.SceneGizmo;
            gizmoRoot.AddComponent(new TransformTranslationGizmoFollowComponent(sceneCamera, gizmoRoot, gizmoMaterial, gizmoHighlightMaterial));

            float4 xAxisOrientation = CreateXAxisOrientation();
            float4 yAxisOrientation = float4.Identity;
            float4 zAxisOrientation = CreateZAxisOrientation();

            CreateAxisEntity(gizmoRoot, "Transform Gizmo X", xAxisOrientation, new float3(0f, ShaftLength, 0f), xShaftModel, xTipModel, gizmoMaterial);
            CreateAxisEntity(gizmoRoot, "Transform Gizmo Y", yAxisOrientation, new float3(0f, ShaftLength, 0f), yShaftModel, yTipModel, gizmoMaterial);
            CreateAxisEntity(gizmoRoot, "Transform Gizmo Z", zAxisOrientation, new float3(0f, ShaftLength, 0f), zShaftModel, zTipModel, gizmoMaterial);
            CreatePlaneEntity(
                gizmoRoot,
                "Transform Gizmo XY Plane",
                float4.Identity,
                new float3(PlaneInset, PlaneInset, 0f),
                xyPlaneModel,
                gizmoMaterial);
            CreatePlaneEntity(
                gizmoRoot,
                "Transform Gizmo XZ Plane",
                CreateXzPlaneOrientation(),
                new float3(PlaneInset, 0f, PlaneInset),
                xzPlaneModel,
                gizmoMaterial);
            CreatePlaneEntity(
                gizmoRoot,
                "Transform Gizmo YZ Plane",
                CreateYzPlaneOrientation(),
                new float3(0f, PlaneInset, PlaneInset),
                yzPlaneModel,
                gizmoMaterial);

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

            for (int i = 0; i < modelAsset.TexCoords.Length; i++) {
                modelAsset.TexCoords[i] = axisMarker;
            }
        }

        /// <summary>
        /// Creates one axis entity containing a shaft and tip mesh.
        /// </summary>
        /// <param name="gizmoRoot">Root entity that owns the axis.</param>
        /// <param name="axisName">Display name for the axis entity.</param>
        /// <param name="axisOrientation">Axis orientation relative to the root.</param>
        /// <param name="tipOffset">Axis-local offset from origin to the cone tip base.</param>
        /// <param name="shaftModel">Runtime mesh used by the shaft.</param>
        /// <param name="tipModel">Runtime mesh used by the cone tip.</param>
        /// <param name="material">Material shared by shaft and tip meshes.</param>
        static void CreateAxisEntity(
            EditorEntity gizmoRoot,
            string axisName,
            float4 axisOrientation,
            float3 tipOffset,
            RuntimeModel shaftModel,
            RuntimeModel tipModel,
            RuntimeMaterial material) {
            if (gizmoRoot == null) {
                throw new ArgumentNullException(nameof(gizmoRoot));
            }

            if (shaftModel == null) {
                throw new ArgumentNullException(nameof(shaftModel));
            }

            if (tipModel == null) {
                throw new ArgumentNullException(nameof(tipModel));
            }

            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            EditorEntity axisEntity = new EditorEntity();
            axisEntity.Name = axisName;
            axisEntity.InternalEntity = true;
            axisEntity.LayerMask = EditorLayerMasks.SceneGizmo;
            axisEntity.Enabled = false;
            axisEntity.Scale = float3.Zero;
            axisEntity.Orientation = axisOrientation;
            axisEntity.AddComponent(new TransformGizmoHandleComponent(new float3(0f, 1f, 0f)));
            gizmoRoot.AddChild(axisEntity);

            EditorEntity shaftEntity = new EditorEntity();
            shaftEntity.Name = string.Concat(axisName, " Shaft");
            shaftEntity.InternalEntity = true;
            shaftEntity.LayerMask = EditorLayerMasks.SceneGizmo;
            shaftEntity.Enabled = false;
            shaftEntity.Scale = float3.Zero;
            MeshComponent shaftMesh = new MeshComponent();
            shaftMesh.Model = shaftModel;
            shaftMesh.Material = material;
            shaftEntity.AddComponent(shaftMesh);
            axisEntity.AddChild(shaftEntity);

            EditorEntity tipEntity = new EditorEntity();
            tipEntity.Name = string.Concat(axisName, " Tip");
            tipEntity.InternalEntity = true;
            tipEntity.LayerMask = EditorLayerMasks.SceneGizmo;
            tipEntity.Enabled = false;
            tipEntity.Scale = float3.Zero;
            tipEntity.Position = tipOffset;
            MeshComponent tipMesh = new MeshComponent();
            tipMesh.Model = tipModel;
            tipMesh.Material = material;
            tipEntity.AddComponent(tipMesh);
            axisEntity.AddChild(tipEntity);
        }

        /// <summary>
        /// Creates one plane handle entity used for planar translation.
        /// </summary>
        /// <param name="gizmoRoot">Root entity that owns the handle.</param>
        /// <param name="handleName">Display name for the plane handle entity.</param>
        /// <param name="handleOrientation">Handle orientation relative to the root.</param>
        /// <param name="handleOffset">Handle local position relative to the root.</param>
        /// <param name="planeModel">Runtime mesh used by the handle.</param>
        /// <param name="material">Material used by the handle mesh.</param>
        static void CreatePlaneEntity(
            EditorEntity gizmoRoot,
            string handleName,
            float4 handleOrientation,
            float3 handleOffset,
            RuntimeModel planeModel,
            RuntimeMaterial material) {
            if (gizmoRoot == null) {
                throw new ArgumentNullException(nameof(gizmoRoot));
            }

            if (planeModel == null) {
                throw new ArgumentNullException(nameof(planeModel));
            }

            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            EditorEntity planeEntity = new EditorEntity();
            planeEntity.Name = handleName;
            planeEntity.InternalEntity = true;
            planeEntity.LayerMask = EditorLayerMasks.SceneGizmo;
            planeEntity.Enabled = false;
            planeEntity.Scale = float3.Zero;
            planeEntity.Position = handleOffset;
            planeEntity.Orientation = handleOrientation;
            planeEntity.AddComponent(new TransformGizmoHandleComponent(new float3(1f, 0f, 0f), new float3(0f, 1f, 0f)));

            MeshComponent planeMesh = new MeshComponent();
            planeMesh.Model = planeModel;
            planeMesh.Material = material;
            planeEntity.AddComponent(planeMesh);

            gizmoRoot.AddChild(planeEntity);
        }

        /// <summary>
        /// Creates the axis rotation that maps +Y geometry into +X direction.
        /// </summary>
        /// <returns>Quaternion rotating +Y to +X.</returns>
        static float4 CreateXAxisOrientation() {
            float3 zAxis = new float3(0f, 0f, 1f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref zAxis, (float)(-Math.PI * 0.5), out orientation);
            return orientation;
        }

        /// <summary>
        /// Creates the axis rotation that maps +Y geometry into +Z direction.
        /// </summary>
        /// <returns>Quaternion rotating +Y to +Z.</returns>
        static float4 CreateZAxisOrientation() {
            float3 xAxis = new float3(1f, 0f, 0f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref xAxis, (float)(Math.PI * 0.5), out orientation);
            return orientation;
        }

        /// <summary>
        /// Creates the plane rotation that maps local XY plane to world XZ plane.
        /// </summary>
        /// <returns>Quaternion rotating local +Y to world +Z.</returns>
        static float4 CreateXzPlaneOrientation() {
            float3 xAxis = new float3(1f, 0f, 0f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref xAxis, (float)(Math.PI * 0.5), out orientation);
            return orientation;
        }

        /// <summary>
        /// Creates the plane rotation that maps local XY plane to world YZ plane.
        /// </summary>
        /// <returns>Quaternion rotating local +X to world +Z and local +Z to world +X.</returns>
        static float4 CreateYzPlaneOrientation() {
            float3 yAxis = new float3(0f, 1f, 0f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref yAxis, (float)(-Math.PI * 0.5), out orientation);
            return orientation;
        }
    }
}

