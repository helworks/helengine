using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies model preview behavior for bounds framing and pointer interaction.
    /// </summary>
    public class ModelPreviewSourceTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the preview tests.
        /// </summary>
        readonly string TempProjectRootPath;
        /// <summary>
        /// Temporary assets root used by the preview tests.
        /// </summary>
        readonly string AssetsRootPath;
        readonly TestGeneratedAssetGraph GeneratedAssetGraph;

        /// <summary>
        /// Initializes the core services required by the model preview tests.
        /// </summary>
        public ModelPreviewSourceTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-model-preview-tests", Guid.NewGuid().ToString("N"));
            AssetsRootPath = Path.Combine(TempProjectRootPath, "assets");
            Directory.CreateDirectory(TempProjectRootPath);
            Directory.CreateDirectory(AssetsRootPath);

            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new helengine.directx11.DirectX11ShaderBackend());
            shaderBackendRegistry.Register(new helengine.vulkan.VulkanShaderBackend());

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempProjectRootPath)
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            GeneratedAssetGraph = new TestGeneratedAssetGraph(core);
        }

        /// <summary>
        /// Deletes temporary content after each test.
        /// </summary>
        public void Dispose() {
            GeneratedAssetGraph.Dispose();
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures resizing the preview updates the render target and camera viewport.
        /// </summary>
        [Fact]
        public void Resize_WhenContentSizeChanges_ResizesTheRenderTargetAndViewport() {
            ModelPreviewSource source = new ModelPreviewSource(CreateRuntimeModel(), Core.Instance.RenderManager3D, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.MaterialCache);

            source.Resize(new int2(640, 360));

            Assert.Equal(640, source.RenderTarget.Width);
            Assert.Equal(360, source.RenderTarget.Height);
            Assert.Equal(new float4(0f, 0f, 640f, 360f), source.PreviewCamera.Viewport);
        }

        /// <summary>
        /// Ensures the preview camera clear color follows the active theme background color.
        /// </summary>
        [Fact]
        public void Constructor_WhenThemeChanges_UsesThemeBackgroundPrimaryForPreviewClearColor() {
            ThemeManager.ThemePalette originalTheme = ThemeManager.Current;
            try {
                ThemeManager.SetTheme(ThemeManager.CreateDarkTheme());
            ModelPreviewSource source = new ModelPreviewSource(CreateRuntimeModel(), Core.Instance.RenderManager3D, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.MaterialCache);

                Assert.Equal(ConvertThemeColor(ThemeManager.Colors.BackgroundPrimary), source.PreviewCamera.ClearSettings.ClearColor);

                source.Dispose();
            } finally {
                ThemeManager.SetTheme(originalTheme);
            }
        }

        /// <summary>
        /// Ensures wheel input moves the camera closer to the model bounds center.
        /// </summary>
        [Fact]
        public void HandleMouseWheel_WhenZoomingIn_MovesTheCameraCloser() {
            ModelPreviewSource source = new ModelPreviewSource(CreateRuntimeModel(), Core.Instance.RenderManager3D, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.MaterialCache);
            source.Resize(new int2(640, 360));
            float3 initialPosition = source.PreviewCamera.Parent.Position;
            double initialDistance = GetDistance(initialPosition, float3.Zero);

            source.HandleMouseWheel(120);
            source.Update();

            float3 zoomedPosition = source.PreviewCamera.Parent.Position;
            double zoomedDistance = GetDistance(zoomedPosition, float3.Zero);

            Assert.True(zoomedDistance < initialDistance);
        }

        /// <summary>
        /// Ensures left-drag input orbits the camera around the model bounds center.
        /// </summary>
        [Fact]
        public void HandleMouseDrag_WhenOrbiting_ChangesTheCameraOrientationWithoutChangingDistance() {
            ModelPreviewSource source = new ModelPreviewSource(CreateRuntimeModel(), Core.Instance.RenderManager3D, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.MaterialCache);
            source.Resize(new int2(640, 360));
            float3 initialPosition = source.PreviewCamera.Parent.Position;
            float4 initialOrientation = source.PreviewCamera.Parent.Orientation;
            double initialDistance = GetDistance(initialPosition, float3.Zero);

            source.HandleMouseDrag(new int2(24, -12));
            source.Update();

            float3 orbitPosition = source.PreviewCamera.Parent.Position;
            float4 orbitOrientation = source.PreviewCamera.Parent.Orientation;
            double orbitDistance = GetDistance(orbitPosition, float3.Zero);

            Assert.NotEqual(initialOrientation, orbitOrientation);
            Assert.NotEqual(initialPosition, orbitPosition);
            Assert.True(Math.Abs(orbitDistance - initialDistance) < 0.0001d);
        }

        /// <summary>
        /// Ensures middle-drag input pans the camera instead of orbiting it.
        /// </summary>
        [Fact]
        public void HandleMouseMiddleDrag_WhenPanning_ChangesTheCameraPositionWithoutChangingOrientation() {
            ModelPreviewSource source = new ModelPreviewSource(CreateRuntimeModel(), Core.Instance.RenderManager3D, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.MaterialCache);
            source.Resize(new int2(640, 360));
            float3 initialPosition = source.PreviewCamera.Parent.Position;
            float4 initialOrientation = source.PreviewCamera.Parent.Orientation;

            source.HandleMouseMiddleDrag(new int2(24, -12));
            source.Update();

            float3 pannedPosition = source.PreviewCamera.Parent.Position;
            float4 pannedOrientation = source.PreviewCamera.Parent.Orientation;

            Assert.Equal(initialOrientation, pannedOrientation);
            Assert.NotEqual(initialPosition, pannedPosition);
        }

        /// <summary>
        /// Ensures tall preview models keep the near plane at the camera minimum while expanding the far plane enough to include the fitted camera distance plus the model radius.
        /// </summary>
        [Fact]
        public void Resize_WhenPreviewModelIsTallerThanDefaultFarPlane_KeepsNearPlaneAtMinimumAndExtendsTheFarPlane() {
            RuntimeModel tallModel = CreateTallRuntimeModel();
            ModelPreviewSource source = new ModelPreviewSource(tallModel, Core.Instance.RenderManager3D, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.MaterialCache);
            source.Resize(new int2(640, 360));
            double radius = Math.Sqrt((11.949154d * 11.949154d) + (113.883775d * 113.883775d) + (12.0684385d * 12.0684385d));
            double cameraDistance = GetDistance(source.PreviewCamera.Parent.Position, float3.Zero);

            Assert.Equal(CameraProjectionUtils.MinimumNearPlaneDistance, source.PreviewCamera.NearPlaneDistance);
            Assert.True(source.PreviewCamera.FarPlaneDistance > cameraDistance + radius);
            source.Dispose();
        }

        /// <summary>
        /// Ensures imported model previews bind the generated diffuse texture instead of the neutral fallback texture.
        /// </summary>
        [Fact]
        public void TryCreate_WhenModelHasImportedMaterials_BindsTheImportedDiffuseTexture() {
            TestModelImporter modelImporter = new TestModelImporter {
                GeneratedMaterials = new[] {
                    CreateGeneratedMaterial("Default", "Materials/Default.hasset", "Textures/Fabric.png")
                }
            };
            AssetImportManager assetImportManager = CreateAssetImportManager(modelImporter);
            string textureSourcePath = WriteSourceFile("Textures/Fabric.png", "texture source");
            assetImportManager.ImportTexture(textureSourcePath);
            Assert.True(assetImportManager.TryLoadTextureAsset(textureSourcePath, out TextureAsset importedTexture));
            Assert.NotNull(importedTexture);
            string modelSourcePath = WriteSourceFile("Models/Preview.mock", "model source");
            AssetBrowserEntry entry = AssetBrowserEntry.CreateFileSystemFile("Preview", "Models/Preview.mock", modelSourcePath, ".mock", AssetEntryKind.Model);

            ModelAssetImportSettings settings = assetImportManager.LoadOrCreateModelImportSettings(modelSourcePath);
            Assert.Equal("test-model", settings.Importer.ImporterId);
            ImportedModelAssetSet importedModel = assetImportManager.ContentManager.Load<ImportedModelAssetSet>(modelSourcePath, settings.Importer.ImporterId);
            Assert.NotNull(importedModel);
            Assert.NotNull(importedModel.ModelAsset);
            Assert.False(entry.IsDirectory);
            Assert.False(entry.IsGenerated);
            Assert.Equal(AssetEntryKind.Model, entry.EntryKind);

            bool created = ModelPreviewSource.TryCreate(entry, assetImportManager, Core.Instance.RenderManager3D, GeneratedAssetGraph.Registry, GeneratedAssetGraph.MaterialCache, GeneratedAssetGraph.ShaderLibrary, out ModelPreviewSource source);

            Assert.True(created);
            MeshComponent previewMesh = GetPrivateField<MeshComponent>(source, "previewMeshComponent");
            ShaderRuntimeMaterial previewMaterial = Assert.IsAssignableFrom<ShaderRuntimeMaterial>(Assert.Single(previewMesh.Materials));
            int diffuseBindingIndex = previewMaterial.Layout.FindTextureBindingIndex(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName);
            Assert.True(diffuseBindingIndex >= 0);
            RuntimeTexture diffuseTexture = previewMaterial.Properties.GetTexture(diffuseBindingIndex);
            Assert.NotNull(diffuseTexture);
            Assert.NotSame(TextureUtils.PixelTexture, diffuseTexture);

            source.Dispose();
        }

        /// <summary>
        /// Ensures imported model previews resolve authored texture paths relative to the model source directory.
        /// </summary>
        [Fact]
        public void TryCreate_WhenImportedTextureLivesBesideTheModel_ResolvesItFromTheModelDirectory() {
            TestModelImporter modelImporter = new TestModelImporter {
                GeneratedMaterials = new[] {
                    CreateGeneratedMaterial("Default", "Materials/Default.hasset", "Textures/Fabric.png")
                }
            };
            AssetImportManager assetImportManager = CreateAssetImportManager(modelImporter);
            string textureSourcePath = WriteSourceFile("Models/Sponza/Textures/Fabric.png", "texture source");
            assetImportManager.ImportTexture(textureSourcePath);
            string modelSourcePath = WriteSourceFile("Models/Sponza/Sponza.mock", "model source");
            AssetBrowserEntry entry = AssetBrowserEntry.CreateFileSystemFile("Sponza", "Models/Sponza/Sponza.mock", modelSourcePath, ".mock", AssetEntryKind.Model);

            bool created = ModelPreviewSource.TryCreate(entry, assetImportManager, Core.Instance.RenderManager3D, GeneratedAssetGraph.Registry, GeneratedAssetGraph.MaterialCache, GeneratedAssetGraph.ShaderLibrary, out ModelPreviewSource source);

            Assert.True(created);
            MeshComponent previewMesh = GetPrivateField<MeshComponent>(source, "previewMeshComponent");
            ShaderRuntimeMaterial previewMaterial = Assert.IsAssignableFrom<ShaderRuntimeMaterial>(Assert.Single(previewMesh.Materials));
            int diffuseBindingIndex = previewMaterial.Layout.FindTextureBindingIndex(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName);
            Assert.True(diffuseBindingIndex >= 0);
            RuntimeTexture diffuseTexture = previewMaterial.Properties.GetTexture(diffuseBindingIndex);
            Assert.NotNull(diffuseTexture);
            Assert.NotSame(TextureUtils.PixelTexture, diffuseTexture);

            source.Dispose();
        }

        /// <summary>
        /// Ensures imported model previews decode source textures directly instead of reusing stale cached texture assets.
        /// </summary>
        [Fact]
        public void TryCreate_WhenTextureCacheIsStale_UsesTheSourceTextureInsteadOfTheCachedAsset() {
            TestModelImporter modelImporter = new TestModelImporter {
                GeneratedMaterials = new[] {
                    CreateGeneratedMaterial("Default", "Materials/Default.hasset", "Textures/Fabric.png")
                }
            };
            AssetImportManager assetImportManager = CreateAssetImportManager(modelImporter);
            string textureSourcePath = WriteSourceFile("Textures/Fabric.png", "texture source");
            assetImportManager.ImportTexture(textureSourcePath);
            TextureAssetImportSettings textureSettings = assetImportManager.LoadOrCreateTextureImportSettings(textureSourcePath);
            string cachedTexturePath = Path.Combine(assetImportManager.ImportRootPath, textureSettings.Importer.AssetId);
            using (FileStream stream = new FileStream(cachedTexturePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, new TextureAsset {
                    Id = textureSettings.Importer.AssetId,
                    Width = 7,
                    Height = 7,
                    Colors = new byte[7 * 7 * 4]
                });
            }

            string modelSourcePath = WriteSourceFile("Models/Preview.mock", "model source");
            AssetBrowserEntry entry = AssetBrowserEntry.CreateFileSystemFile("Preview", "Models/Preview.mock", modelSourcePath, ".mock", AssetEntryKind.Model);

            bool created = ModelPreviewSource.TryCreate(entry, assetImportManager, Core.Instance.RenderManager3D, GeneratedAssetGraph.Registry, GeneratedAssetGraph.MaterialCache, GeneratedAssetGraph.ShaderLibrary, out ModelPreviewSource source);

            Assert.True(created);
            MeshComponent previewMesh = GetPrivateField<MeshComponent>(source, "previewMeshComponent");
            ShaderRuntimeMaterial previewMaterial = Assert.IsAssignableFrom<ShaderRuntimeMaterial>(Assert.Single(previewMesh.Materials));
            int diffuseBindingIndex = previewMaterial.Layout.FindTextureBindingIndex(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName);
            Assert.True(diffuseBindingIndex >= 0);
            RuntimeTexture diffuseTexture = previewMaterial.Properties.GetTexture(diffuseBindingIndex);
            Assert.NotNull(diffuseTexture);
            Assert.Equal(1, diffuseTexture.Width);
            Assert.Equal(1, diffuseTexture.Height);

            source.Dispose();
        }

        /// <summary>
        /// Ensures imported materials without authored diffuse textures still bind a dedicated neutral preview texture instead of the default white pixel.
        /// </summary>
        [Fact]
        public void TryCreate_WhenImportedMaterialHasNoDiffuseTexture_BindsNeutralPreviewTexture() {
            TestModelImporter modelImporter = new TestModelImporter {
                GeneratedMaterials = new[] {
                    CreateGeneratedMaterial("Default", "Materials/Default.hasset", string.Empty)
                }
            };
            AssetImportManager assetImportManager = CreateAssetImportManager(modelImporter);
            string modelSourcePath = WriteSourceFile("Models/Lamppost.mock", "model source");
            AssetBrowserEntry entry = AssetBrowserEntry.CreateFileSystemFile("Lamppost", "Models/Lamppost.mock", modelSourcePath, ".mock", AssetEntryKind.Model);

            bool created = ModelPreviewSource.TryCreate(entry, assetImportManager, Core.Instance.RenderManager3D, GeneratedAssetGraph.Registry, GeneratedAssetGraph.MaterialCache, GeneratedAssetGraph.ShaderLibrary, out ModelPreviewSource source);

            Assert.True(created);
            MeshComponent previewMesh = GetPrivateField<MeshComponent>(source, "previewMeshComponent");
            ShaderRuntimeMaterial previewMaterial = Assert.IsAssignableFrom<ShaderRuntimeMaterial>(Assert.Single(previewMesh.Materials));
            int diffuseBindingIndex = previewMaterial.Layout.FindTextureBindingIndex(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName);
            Assert.True(diffuseBindingIndex >= 0);
            RuntimeTexture diffuseTexture = previewMaterial.Properties.GetTexture(diffuseBindingIndex);
            Assert.NotNull(diffuseTexture);
            Assert.NotSame(TextureUtils.PixelTexture, diffuseTexture);

            source.Dispose();
        }

        /// <summary>
        /// Ensures the preview model is isolated from the main viewport camera by using a dedicated preview layer.
        /// </summary>
        [Fact]
        public void Constructor_WhenPreviewSourceIsCreated_KeepsTheModelOutOfTheMainViewportQueue() {
            EditorEntity mainCameraEntity = new EditorEntity();
            CameraComponent mainCamera = new CameraComponent {
                LayerMask = EditorLayerMasks.SceneObjects,
                CameraDrawOrder = 0,
                Viewport = new float4(0f, 0f, 640f, 360f)
            };
            mainCameraEntity.AddComponent(mainCamera);

            ModelPreviewSource source = new ModelPreviewSource(CreateRuntimeModel(), Core.Instance.RenderManager3D, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.MaterialCache);
            MeshComponent previewMesh = GetPrivateField<MeshComponent>(source, "previewMeshComponent");

            Assert.False(QueueContainsDrawable(mainCamera.RenderQueue3D, previewMesh));
            Assert.True(QueueContainsDrawable(source.PreviewCamera.RenderQueue3D, previewMesh));

            source.Dispose();
            mainCameraEntity.Dispose();
        }

        /// <summary>
        /// Ensures compact models receive the five-unit minimum floor grid beneath their centered preview transform.
        /// </summary>
        [Fact]
        public void Constructor_WhenModelBoundsAreSmallerThanFiveUnits_CreatesFiveUnitPreviewGrid() {
            TestRenderManager3D renderManager3D = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            RuntimeModel runtimeModel = CreateRuntimeModel();
            int gridModelAssetIndex = renderManager3D.BuiltModelAssets.Count;
            ModelPreviewSource source = new ModelPreviewSource(runtimeModel, Core.Instance.RenderManager3D, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.MaterialCache);

            EditorEntity gridEntity = GetPrivateField<EditorEntity>(source, "previewGridEntity");
            MeshComponent gridMesh = Assert.IsType<MeshComponent>(Assert.Single(gridEntity.Components, component => component is MeshComponent));
            ModelAsset gridModelAsset = renderManager3D.BuiltModelAssets[gridModelAssetIndex];

            Assert.True(source.IsGridVisible);
            Assert.NotNull(gridMesh.Model);
            Assert.Contains(gridModelAsset.Positions, position => position.Equals(new float3(-2.5f, -2.5f, 0f)));
            Assert.Contains(gridModelAsset.Positions, position => position.Equals(new float3(2.5f, 2.5f, 0f)));
            Assert.Equal(float3.One, gridEntity.LocalScale);
            Assert.Equal(new float3(0f, -1.001f, 0f), gridEntity.LocalPosition);
            source.Dispose();
        }

        /// <summary>
        /// Ensures wide models expand the preview grid to their largest horizontal bound extent.
        /// </summary>
        [Fact]
        public void Constructor_WhenModelIsWiderThanFiveUnits_ScalesPreviewGridToModelWidth() {
            TestRenderManager3D renderManager3D = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            RuntimeModel runtimeModel = CreateWideRuntimeModel();
            int gridModelAssetIndex = renderManager3D.BuiltModelAssets.Count;
            ModelPreviewSource source = new ModelPreviewSource(runtimeModel, Core.Instance.RenderManager3D, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.MaterialCache);

            EditorEntity gridEntity = GetPrivateField<EditorEntity>(source, "previewGridEntity");
            MeshComponent gridMesh = Assert.IsType<MeshComponent>(Assert.Single(gridEntity.Components, component => component is MeshComponent));
            ModelAsset gridModelAsset = renderManager3D.BuiltModelAssets[gridModelAssetIndex];

            Assert.NotNull(gridMesh.Model);
            Assert.Contains(gridModelAsset.Positions, position => position.Equals(new float3(-8f, -8f, 0f)));
            Assert.Contains(gridModelAsset.Positions, position => position.Equals(new float3(8f, 8f, 0f)));
            Assert.Equal(new float3(0f, -2.001f, 0f), gridEntity.LocalPosition);
            source.Dispose();
        }

        /// <summary>
        /// Ensures grid visibility updates both the public source state and rendered grid entity.
        /// </summary>
        [Fact]
        public void SetGridVisible_WhenDisabled_HidesThePreviewGridEntity() {
            ModelPreviewSource source = new ModelPreviewSource(CreateRuntimeModel(), Core.Instance.RenderManager3D, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.MaterialCache);

            source.SetGridVisible(false);

            Assert.False(source.IsGridVisible);
            Assert.False(GetPrivateField<EditorEntity>(source, "previewGridEntity").Enabled);
            source.Dispose();
        }

        /// <summary>
        /// Ensures model previews initialize with no bounds overlay and enable only the requested wireframe box or sphere.
        /// </summary>
        [Fact]
        public void SetBoundsDisplayMode_WhenModeChanges_EnablesOnlyTheRequestedLineOverlay() {
            ModelPreviewSource source = new ModelPreviewSource(CreateRuntimeModel(), Core.Instance.RenderManager3D, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.MaterialCache);
            EditorEntity boundsBoxEntity = GetPrivateField<EditorEntity>(source, "boundsBoxEntity");
            EditorEntity boundsSphereEntity = GetPrivateField<EditorEntity>(source, "boundsSphereEntity");

            Assert.Equal(ModelPreviewBoundsDisplayMode.None, source.BoundsDisplayMode);
            Assert.False(boundsBoxEntity.Enabled);
            Assert.False(boundsSphereEntity.Enabled);

            source.SetBoundsDisplayMode(ModelPreviewBoundsDisplayMode.Box);

            Assert.Equal(ModelPreviewBoundsDisplayMode.Box, source.BoundsDisplayMode);
            Assert.True(boundsBoxEntity.Enabled);
            Assert.False(boundsSphereEntity.Enabled);

            source.SetBoundsDisplayMode(ModelPreviewBoundsDisplayMode.Sphere);

            Assert.Equal(ModelPreviewBoundsDisplayMode.Sphere, source.BoundsDisplayMode);
            Assert.False(boundsBoxEntity.Enabled);
            Assert.True(boundsSphereEntity.Enabled);
            source.Dispose();
        }

        /// <summary>
        /// Ensures both model-bounds overlays use line-list geometry instead of solid triangle meshes.
        /// </summary>
        [Fact]
        public void Constructor_WhenBoundsOverlaysAreCreated_UsesLineListSubmeshes() {
            ModelPreviewSource source = new ModelPreviewSource(CreateRuntimeModel(), Core.Instance.RenderManager3D, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.MaterialCache);
            EditorEntity boundsBoxEntity = GetPrivateField<EditorEntity>(source, "boundsBoxEntity");
            EditorEntity boundsSphereEntity = GetPrivateField<EditorEntity>(source, "boundsSphereEntity");
            MeshComponent boxMesh = Assert.IsType<MeshComponent>(Assert.Single(boundsBoxEntity.Components, component => component is MeshComponent));
            MeshComponent sphereMesh = Assert.IsType<MeshComponent>(Assert.Single(boundsSphereEntity.Components, component => component is MeshComponent));

            RuntimeSubmesh boxSubmesh = Assert.Single(boxMesh.Model.Submeshes);
            RuntimeSubmesh sphereSubmesh = Assert.Single(sphereMesh.Model.Submeshes);

            Assert.Equal(ModelPrimitiveTopology.LineList, boxSubmesh.PrimitiveTopology);
            Assert.Equal(ModelPrimitiveTopology.LineList, sphereSubmesh.PrimitiveTopology);
            source.Dispose();
        }

        /// <summary>
        /// Ensures box mode displays the three dimensions on positive-facing bounds edges through the gizmo label material.
        /// </summary>
        [Fact]
        public void ConfigureBoundsDimensionLabels_WhenBoxModeIsSelected_ShowsThreeGizmoFontBillboardsAtPositiveEdges() {
            ModelPreviewSource source = new ModelPreviewSource(CreateRuntimeModel(), Core.Instance.RenderManager3D, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.MaterialCache);
            FontAsset font = CreateAxisLabelFont();

            source.ConfigureBoundsDimensionLabels(font);
            source.SetBoundsDisplayMode(ModelPreviewBoundsDisplayMode.Box);

            EditorEntity[] labels = GetPrivateField<EditorEntity[]>(source, "boundsDimensionLabelEntities");
            Assert.Equal(3, labels.Length);
            Assert.All(labels, label => Assert.True(label.Enabled));
            Assert.Equal(new float3(0f, 1f, 1f), labels[0].LocalPosition);
            Assert.Equal(new float3(1f, 0f, 1f), labels[1].LocalPosition);
            Assert.Equal(new float3(1f, 1f, 0f), labels[2].LocalPosition);
            Assert.All(labels, label => {
                MeshComponent mesh = Assert.IsType<MeshComponent>(Assert.Single(label.Components, component => component is MeshComponent));
                ShaderRuntimeMaterial material = Assert.IsAssignableFrom<ShaderRuntimeMaterial>(Assert.Single(mesh.Materials));
                int labelTextureIndex = material.Layout.FindTextureBindingIndex("LabelTexture");
                Assert.True(labelTextureIndex >= 0);
                Assert.Same(font.Texture, material.Properties.GetTexture(labelTextureIndex));
            });

            source.HandleMouseDrag(new int2(12, -6));
            source.Update();
            Assert.All(labels, label => {
                Assert.Equal(source.PreviewCamera.Parent.Orientation, label.Orientation);
                Assert.True(label.Scale.X > 0f);
                Assert.Equal(label.Scale.X, label.Scale.Y);
                Assert.Equal(label.Scale.X, label.Scale.Z);
            });

            source.SetBoundsDisplayMode(ModelPreviewBoundsDisplayMode.Sphere);
            Assert.All(labels, label => Assert.False(label.Enabled));
            source.Dispose();
        }

        /// <summary>
        /// Builds one simple runtime model with known cached bounds for preview framing tests.
        /// </summary>
        /// <returns>Runtime model with deterministic bounds.</returns>
        RuntimeModel CreateRuntimeModel() {
            ModelAsset modelAsset = new ModelAsset {
                Positions = new[] {
                    new float3(-1f, -1f, -1f),
                    new float3(1f, -1f, -1f),
                    new float3(1f, 1f, -1f),
                    new float3(-1f, 1f, -1f)
                },
                Normals = new[] {
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f)
                },
                TexCoords = new[] {
                    new float2(0f, 0f),
                    new float2(1f, 0f),
                    new float2(1f, 1f),
                    new float2(0f, 1f)
                },
                Submeshes = new[] {
                    new ModelSubmeshAsset {
                        IndexStart = 0,
                        IndexCount = 6,
                        MaterialSlotName = "Default"
                    }
                },
                Indices16 = new ushort[] { 0, 1, 2, 0, 2, 3 },
                BoundsMin = new float3(-1f, -1f, -1f),
                BoundsMax = new float3(1f, 1f, 1f)
            };

            return Core.Instance.RenderManager3D.BuildModelFromRaw(modelAsset);
        }

        /// <summary>
        /// Builds one tall runtime model whose fitted preview distance exceeds the default camera far plane.
        /// </summary>
        /// <returns>Runtime model with lamppost-like bounds.</returns>
        RuntimeModel CreateTallRuntimeModel() {
            ModelAsset modelAsset = new ModelAsset {
                Positions = new[] {
                    new float3(-33.212997f, 0.015032411f, -11.019729f),
                    new float3(-9.314689f, 0.015032411f, -11.019729f),
                    new float3(-9.314689f, 227.78258f, 13.117148f),
                    new float3(-33.212997f, 227.78258f, 13.117148f)
                },
                Normals = new[] {
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f)
                },
                TexCoords = new[] {
                    new float2(0f, 0f),
                    new float2(1f, 0f),
                    new float2(1f, 1f),
                    new float2(0f, 1f)
                },
                Submeshes = new[] {
                    new ModelSubmeshAsset {
                        IndexStart = 0,
                        IndexCount = 6,
                        MaterialSlotName = "Default"
                    }
                },
                Indices16 = new ushort[] { 0, 1, 2, 0, 2, 3 },
                BoundsMin = new float3(-33.212997f, 0.015032411f, -11.019729f),
                BoundsMax = new float3(-9.314689f, 227.78258f, 13.117148f)
            };

            return Core.Instance.RenderManager3D.BuildModelFromRaw(modelAsset);
        }

        /// <summary>
        /// Creates a compact font asset containing every glyph required by formatted preview dimensions.
        /// </summary>
        /// <returns>Font asset backed by a deterministic test texture.</returns>
        FontAsset CreateAxisLabelFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['0'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['1'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['2'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['3'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['4'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['5'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['6'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['7'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['8'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['9'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['.'] = new FontChar(new float4(0f, 0f, 4f, 4f), 0f, 4f, 0f, 0f),
                ['-'] = new FontChar(new float4(0f, 0f, 6f, 3f), 0f, 6f, 0f, 0f)
            };
            return new FontAsset(
                new FontInfo("Axis Label", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
        }

        /// <summary>
        /// Builds one wide runtime model used to verify preview-grid sizing from horizontal bounds.
        /// </summary>
        /// <returns>Runtime model with a sixteen-unit horizontal extent.</returns>
        RuntimeModel CreateWideRuntimeModel() {
            ModelAsset modelAsset = new ModelAsset {
                Positions = new[] {
                    new float3(-8f, 0f, -1f),
                    new float3(8f, 0f, -1f),
                    new float3(8f, 4f, 1f),
                    new float3(-8f, 4f, 1f)
                },
                Normals = new[] {
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f)
                },
                TexCoords = new[] {
                    new float2(0f, 0f),
                    new float2(1f, 0f),
                    new float2(1f, 1f),
                    new float2(0f, 1f)
                },
                Submeshes = new[] {
                    new ModelSubmeshAsset {
                        IndexStart = 0,
                        IndexCount = 6,
                        MaterialSlotName = "Default"
                    }
                },
                Indices16 = new ushort[] { 0, 1, 2, 0, 2, 3 },
                BoundsMin = new float3(-8f, 0f, -1f),
                BoundsMax = new float3(8f, 4f, 1f)
            };

            return Core.Instance.RenderManager3D.BuildModelFromRaw(modelAsset);
        }

        /// <summary>
        /// Creates one configured asset import manager for preview source tests.
        /// </summary>
        /// <param name="modelImporter">Model importer used by the preview test.</param>
        /// <returns>Configured asset import manager.</returns>
        AssetImportManager CreateAssetImportManager(TestModelImporter modelImporter) {
            if (modelImporter == null) {
                throw new ArgumentNullException(nameof(modelImporter));
            }

            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(AssetsRootPath));
            AssetImportManager assetImportManager = new AssetImportManager(TempProjectRootPath, contentManager);
            assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), new[] { ".png" }));
            assetImportManager.RegisterModelImporter(new ModelImporterRegistration("test-model", modelImporter, new[] { ".mock" }));
            assetImportManager.SetDefaultTextureImporter(".png", "test-texture");
            assetImportManager.SetDefaultModelImporter(".mock", "test-model");
            return assetImportManager;
        }

        /// <summary>
        /// Writes one source file inside the temporary assets folder.
        /// </summary>
        /// <param name="relativePath">Project-relative path for the source file.</param>
        /// <param name="contents">File contents to write.</param>
        /// <returns>Absolute path to the written source file.</returns>
        string WriteSourceFile(string relativePath, string contents) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            } else if (string.IsNullOrWhiteSpace(contents)) {
                throw new ArgumentException("Contents must be provided.", nameof(contents));
            }

            string fullPath = Path.Combine(AssetsRootPath, relativePath);
            string directoryPath = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directoryPath)) {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllText(fullPath, contents);
            return fullPath;
        }

        /// <summary>
        /// Creates one generated material description used by the imported-model preview test.
        /// </summary>
        /// <param name="materialName">Stable material name resolved from the importer.</param>
        /// <param name="relativePath">Relative output path for the generated material.</param>
        /// <param name="diffuseTextureAssetId">Imported texture asset identifier used by the material.</param>
        /// <returns>Generated material description for the preview test.</returns>
        ImportedModelMaterialAsset CreateGeneratedMaterial(string materialName, string relativePath, string diffuseTextureAssetId) {
            if (string.IsNullOrWhiteSpace(materialName)) {
                throw new ArgumentException("Material name must be provided.", nameof(materialName));
            } else if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            return new ImportedModelMaterialAsset(
                materialName,
                relativePath,
                new ShaderMaterialAsset {
                    Id = relativePath,
                    ShaderAssetId = BuiltInMaterialIds.StandardMaterialShaderAssetId,
                    VertexProgram = "ForwardStandardShader.vs",
                    PixelProgram = "ForwardStandardShader.ps",
                    Variant = "default",
                    DiffuseTextureAssetId = diffuseTextureAssetId,
                    RenderState = new MaterialRenderState()
                });
        }

        /// <summary>
        /// Converts one byte-based theme color into the normalized float representation used by camera clear settings.
        /// </summary>
        /// <param name="color">Theme color to convert.</param>
        /// <returns>Normalized RGBA color.</returns>
        float4 ConvertThemeColor(byte4 color) {
            return new float4(
                color.X / 255f,
                color.Y / 255f,
                color.Z / 255f,
                color.W / 255f);
        }

        /// <summary>
        /// Measures the distance from one point to the origin.
        /// </summary>
        /// <param name="position">Position to measure.</param>
        /// <param name="target">Target point.</param>
        /// <returns>Euclidean distance between both points.</returns>
        double GetDistance(float3 position, float3 target) {
            double dx = position.X - target.X;
            double dy = position.Y - target.Y;
            double dz = position.Z - target.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// Determines whether one render queue contains the requested drawable.
        /// </summary>
        /// <param name="renderQueue">Render queue to inspect.</param>
        /// <param name="drawable">Drawable expected to be present.</param>
        /// <returns>True when the drawable was visited by the render queue.</returns>
        bool QueueContainsDrawable(IRenderQueue3D renderQueue, IDrawable3D drawable) {
            if (renderQueue == null) {
                throw new ArgumentNullException(nameof(renderQueue));
            }
            if (drawable == null) {
                throw new ArgumentNullException(nameof(drawable));
            }

            RenderQueueContainsVisitor visitor = new RenderQueueContainsVisitor(drawable);
            renderQueue.VisitOrdered(visitor);
            return visitor.Found;
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            System.Reflection.FieldInfo field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Visitor that detects whether a specific drawable is present in one render queue.
        /// </summary>
        sealed class RenderQueueContainsVisitor : IRenderVisitor3D {
            /// <summary>
            /// Drawable that the visitor searches for.
            /// </summary>
            readonly IDrawable3D targetDrawable;

            /// <summary>
            /// Initializes a new render-queue presence visitor.
            /// </summary>
            /// <param name="targetDrawable">Drawable expected to appear in the queue.</param>
            public RenderQueueContainsVisitor(IDrawable3D targetDrawable) {
                this.targetDrawable = targetDrawable;
            }

            /// <summary>
            /// Gets a value indicating whether the target drawable was encountered.
            /// </summary>
            public bool Found { get; private set; }

            /// <summary>
            /// Visits one drawable and records whether it matches the target.
            /// </summary>
            /// <param name="drawable">Drawable encountered during queue traversal.</param>
            public void Visit(IDrawable3D drawable) {
                if (ReferenceEquals(drawable, targetDrawable)) {
                    Found = true;
                }
            }
        }
    }
}

