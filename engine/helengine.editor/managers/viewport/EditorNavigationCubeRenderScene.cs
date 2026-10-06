namespace helengine.editor {
    /// <summary>Owns a private GPU-rendered navigation cube, camera, depth buffer, and color target.</summary>
    public sealed class EditorNavigationCubeRenderScene : IDisposable {
        /// <summary>Renderer owning mesh, material, and target resources.</summary>
        readonly RenderManager3D Renderer;
        /// <summary>Texture allocator for the six solid face colors.</summary>
        readonly RenderManager2D TextureRenderer;
        /// <summary>Projection model shared with the overlay's geometry calculations.</summary>
        readonly EditorViewportNavigationCubeGeometry Geometry = new EditorViewportNavigationCubeGeometry();
        /// <summary>Hidden camera entity, isolated from authored scene content.</summary>
        readonly EditorEntity CameraEntity;
        /// <summary>Hidden faces exclusively bound to this preview camera.</summary>
        readonly List<EditorEntity> Faces = new List<EditorEntity>();
        /// <summary>Owned mesh resources released after their entities.</summary>
        readonly List<RuntimeModel> Models = new List<RuntimeModel>();
        /// <summary>Owned face materials released after their entities.</summary>
        readonly List<RuntimeMaterial> Materials = new List<RuntimeMaterial>();
        /// <summary>Owned single-pixel face textures.</summary>
        readonly List<RuntimeTexture> Textures = new List<RuntimeTexture>();
        /// <summary>Prevents repeated renderer resource disposal.</summary>
        bool IsDisposed;

        /// <summary>Creates six physical faces and an isolated camera matching cube picking geometry.</summary>
        /// <param name="owner">Viewport owning the preview lifetime and interaction services.</param>
        /// <param name="shaders">Session shader library.</param>
        /// <param name="colors">Six signed-axis face colors.</param>
        /// <param name="size">Square render-target resolution.</param>
        public EditorNavigationCubeRenderScene(EditorEntity owner, EditorBuiltInShaderAssetLibrary shaders, byte4[] colors, int size) {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(shaders);
            ArgumentNullException.ThrowIfNull(colors);
            if (colors.Length != 6 || size <= 0) {
                throw new ArgumentException("The cube requires six face colors and a positive target size.");
            }
            Renderer = owner.OwnerCore.RenderManager3D;
            TextureRenderer = owner.OwnerCore.RenderManager2D;
            Target = Renderer.CreateRenderTarget(size, size);
            CameraEntity = CreateEntity(owner, "Navigation Cube Camera");
            Camera = new EditorViewportCameraComponent {
                LayerMask = EditorLayerMasks.NavigationCubePreview,
                RenderTarget = Target,
                Viewport = new float4(0, 0, size, size),
                ProjectionMode = CameraProjectionMode.Orthographic,
                NearPlaneDistance = 0.1f, FarPlaneDistance = 10f,
                ClearSettings = new CameraClearSettings(true, new float4(0, 0, 0, 0), true, 1f, false, 0)
            };
            CameraEntity.AddComponent(Camera);
            try {
                for (int face = 0; face < 6; face++) {
                    CreateFace(owner, shaders, colors[face], face);
                }
                Synchronize(float4.Identity, true);
            } catch {
                Dispose();
                throw;
            }
        }

        /// <summary>Gets the GPU color target shown by the viewport overlay.</summary>
        public RenderTarget Target { get; }
        /// <summary>Gets the isolated 3D camera and its exclusively bound face queue.</summary>
        public EditorViewportCameraComponent Camera { get; }

        /// <summary>Rotates the camera around the physical cube while keeping projection and picking aligned.</summary>
        /// <param name="orientation">Scene camera orientation represented by the cube.</param>
        /// <param name="visible">Whether this viewport currently displays the cube.</param>
        /// <param name="projectionMode">Projection mode of the scene viewport.</param>
        public void Synchronize(float4 orientation, bool visible, CameraProjectionMode projectionMode = CameraProjectionMode.Orthographic) {
            CameraEntity.Enabled = visible;
            CameraEntity.LocalOrientation = orientation;
            CameraEntity.LocalPosition = float4.RotateVector(new float3(0, 0, EditorViewportNavigationCubeGeometry.CameraDistance), orientation);
            Geometry.ProjectionMode = projectionMode;
            float span = Geometry.GetProjectionSpan(orientation);
            Camera.ProjectionMode = projectionMode;
            if (projectionMode == CameraProjectionMode.Perspective) {
                Camera.FieldOfView = (float)(2.0 * Math.Atan(span * 0.5));
            } else {
                Camera.OrthographicVerticalSpan = span;
            }
        }

        /// <summary>Creates one colored physical cube face with depth testing and depth writing enabled.</summary>
        /// <param name="owner">Viewport providing the session graph.</param>
        /// <param name="shaders">Session-owned shader library.</param>
        /// <param name="color">Face color.</param>
        /// <param name="face">Signed-axis face index.</param>
        void CreateFace(EditorEntity owner, EditorBuiltInShaderAssetLibrary shaders, byte4 color, int face) {
            using TextureAsset textureAsset = new TextureAsset { Width = 1, Height = 1, Colors = [color.X, color.Y, color.Z, color.W] };
            RuntimeTexture texture = TextureRenderer.BuildTextureFromRaw(textureAsset);
            Textures.Add(texture);
            RuntimeMaterial material = EditorWorldSpaceSpritePreviewMaterialFactory.Create(Renderer, texture, shaders);
            material.RenderState.BlendMode = MaterialBlendMode.Opaque;
            material.RenderState.DepthWriteEnabled = true;
            Materials.Add(material);
            float3[] vertices = new float3[4];
            float3 normal = default;
            int axis = face / 2;
            float sign = (face & 1) == 0 ? -1f : 1f;
            for (int corner = 0; corner < 4; corner++) {
                float u = (corner == 0 || corner == 3) ? -0.5f : 0.5f;
                float v = corner < 2 ? -0.5f : 0.5f;
                vertices[corner] = axis == 0 ? new float3(sign * 0.5f, u, v)
                    : axis == 1 ? new float3(u, sign * 0.5f, v) : new float3(u, v, sign * 0.5f);
            }
            normal = axis == 0 ? new float3(sign, 0, 0) : axis == 1 ? new float3(0, sign, 0) : new float3(0, 0, sign);
            using ModelAsset modelAsset = new ModelAsset {
                Positions = vertices, Normals = [normal, normal, normal, normal],
                TexCoords = [new float2(0, 0), new float2(1, 0), new float2(1, 1), new float2(0, 1)],
                Indices16 = [0, 1, 2, 0, 2, 3]
            };
            RuntimeModel model = Renderer.BuildModelFromRaw(modelAsset);
            Models.Add(model);
            EditorEntity entity = CreateEntity(owner, "Navigation Cube Face");
            Faces.Add(entity);
            entity.AddComponent(new ViewportComponent { BindingMode = ViewportComponent.ExplicitCameraBindingMode, BoundCameraComponent = Camera });
            entity.AddComponent(new MeshComponent { Model = model, Materials = [material] });
        }

        /// <summary>Creates an internal preview entity using the owning viewport's session services.</summary>
        /// <param name="owner">Owning viewport.</param>
        /// <param name="name">Internal entity role.</param>
        /// <returns>Initialized editor entity.</returns>
        static EditorEntity CreateEntity(EditorEntity owner, string name) {
            return new EditorEntity(owner.OwnerCore, owner.InteractionServices) {
                Name = name, InternalEntity = true, LayerMask = EditorLayerMasks.NavigationCubePreview
            };
        }

        /// <summary>Unregisters hidden entities before releasing their GPU resources.</summary>
        public void Dispose() {
            if (IsDisposed) { return; }
            IsDisposed = true;
            foreach (EditorEntity face in Faces) { face.Dispose(); }
            CameraEntity.Dispose();
            foreach (RuntimeModel model in Models) { Renderer.ReleaseModel(model); }
            foreach (RuntimeMaterial material in Materials) { Renderer.ReleaseMaterial(material); }
            foreach (RuntimeTexture texture in Textures) { TextureRenderer.ReleaseTexture(texture); }
            if (Target is IDisposable disposable) { disposable.Dispose(); }
        }
    }
}
