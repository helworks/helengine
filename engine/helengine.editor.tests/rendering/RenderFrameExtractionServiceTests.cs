using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies shared render-frame extraction and backend capability contracts.
    /// </summary>
    public class RenderFrameExtractionServiceTests {
        /// <summary>
        /// Ensures the extraction service returns one frame entry for one active camera and exposes backend capability metadata through the render manager contract.
        /// </summary>
        [Fact]
        public void Extract_WhenOneCameraExists_ReturnsFrameForThatCamera() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            TestRenderManager3D renderManager = new TestRenderManager3D();
            RenderFrameExtractionService extractionService = new RenderFrameExtractionService();
            CameraComponent camera = new CameraComponent();

            RenderFrameExtractionResult result = extractionService.Extract(
                new[] { camera },
                Array.Empty<IDrawable3D>(),
                Array.Empty<LightComponent>(),
                renderManager.GetCapabilityProfile());

            RenderFrame frame = Assert.Single(result.Frames);
            Assert.Same(camera, frame.Camera);
            Assert.Empty(frame.DrawableSubmissions);
            Assert.Empty(frame.LightSubmissions);
            Assert.Empty(frame.ShadowCasterSubmissions);
            Assert.True(result.BackendCapabilities.SupportsForwardRendering);
        }

        /// <summary>
        /// Ensures extraction preserves both opaque and transparent drawables and marks transparency from material blend state.
        /// </summary>
        [Fact]
        public void Extract_WhenOpaqueAndTransparentDrawablesExist_ReturnsBothSubmissionsWithTransparencyFlags() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            CameraComponent camera = new CameraComponent();
            TestDrawable3D opaqueDrawable = new TestDrawable3D(MaterialBlendMode.Opaque);
            TestDrawable3D transparentDrawable = new TestDrawable3D(MaterialBlendMode.AlphaBlend);
            RenderFrameExtractionService extractionService = new RenderFrameExtractionService();

            RenderFrameExtractionResult result = extractionService.Extract(
                new[] { camera },
                new IDrawable3D[] { opaqueDrawable, transparentDrawable },
                Array.Empty<LightComponent>(),
                new RendererBackendCapabilityProfile(true, false, true, true, 32, 4));

            RenderFrame frame = Assert.Single(result.Frames);
            Assert.Equal(2, frame.DrawableSubmissions.Count);
            Assert.False(frame.DrawableSubmissions[0].IsTransparent);
            Assert.True(frame.DrawableSubmissions[1].IsTransparent);
            Assert.True(frame.HasTransparentDrawables);
        }

        /// <summary>
        /// Ensures only opaque drawables become shadow-caster submissions during frame extraction.
        /// </summary>
        [Fact]
        public void Extract_WhenOpaqueAndTransparentDrawablesExist_ReturnsOnlyOpaqueShadowCasterSubmissions() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            CameraComponent camera = new CameraComponent();
            TestDrawable3D opaqueDrawable = new TestDrawable3D(MaterialBlendMode.Opaque);
            TestDrawable3D transparentDrawable = new TestDrawable3D(MaterialBlendMode.AlphaBlend);
            RenderFrameExtractionService extractionService = new RenderFrameExtractionService();

            RenderFrameExtractionResult result = extractionService.Extract(
                new[] { camera },
                new IDrawable3D[] { opaqueDrawable, transparentDrawable },
                Array.Empty<LightComponent>(),
                new RendererBackendCapabilityProfile(true, false, true, true, 32, 4));

            RenderFrame frame = Assert.Single(result.Frames);
            RenderFrameShadowCasterSubmission shadowCasterSubmission = Assert.Single(frame.ShadowCasterSubmissions);
            Assert.Same(opaqueDrawable, shadowCasterSubmission.Drawable);
        }

        /// <summary>
        /// Ensures drawables without runtime materials are treated as opaque submissions.
        /// </summary>
        [Fact]
        public void Extract_WhenDrawableHasNoMaterial_TreatsItAsOpaque() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            CameraComponent camera = new CameraComponent();
            TestDrawable3D drawable = new TestDrawable3D(MaterialBlendMode.Opaque);
            drawable.Materials = Array.Empty<RuntimeMaterial>();
            RenderFrameExtractionService extractionService = new RenderFrameExtractionService();

            RenderFrameExtractionResult result = extractionService.Extract(
                new[] { camera },
                new IDrawable3D[] { drawable },
                Array.Empty<LightComponent>(),
                new RendererBackendCapabilityProfile(true, false, true, true, 32, 4));

            RenderFrame frame = Assert.Single(result.Frames);
            RenderFrameDrawableSubmission submission = Assert.Single(frame.DrawableSubmissions);
            Assert.False(submission.IsTransparent);
        }

        /// <summary>
        /// Ensures one drawable with mixed opaque and transparent submesh materials is split into independent render-frame submissions.
        /// </summary>
        [Fact]
        public void Extract_WhenDrawableContainsMixedSubmeshMaterials_SplitsOpaqueAndTransparentSubmissions() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            CameraComponent camera = new CameraComponent();
            TestDrawable3D drawable = new TestDrawable3D(
                new[] { MaterialBlendMode.Opaque, MaterialBlendMode.AlphaBlend },
                new[] {
                    new RuntimeSubmesh {
                        MaterialSlotName = "Walls",
                        IndexStart = 0,
                        IndexCount = 12
                    },
                    new RuntimeSubmesh {
                        MaterialSlotName = "Glass",
                        IndexStart = 12,
                        IndexCount = 6
                    }
                });
            RenderFrameExtractionService extractionService = new RenderFrameExtractionService();

            RenderFrameExtractionResult result = extractionService.Extract(
                new[] { camera },
                new IDrawable3D[] { drawable },
                Array.Empty<LightComponent>(),
                new RendererBackendCapabilityProfile(true, false, true, true, 32, 4));

            RenderFrame frame = Assert.Single(result.Frames);
            Assert.Equal(2, frame.DrawableSubmissions.Count);
            Assert.Equal(0, frame.DrawableSubmissions[0].SubmeshIndex);
            Assert.False(frame.DrawableSubmissions[0].IsTransparent);
            Assert.Equal(1, frame.DrawableSubmissions[1].SubmeshIndex);
            Assert.True(frame.DrawableSubmissions[1].IsTransparent);
            RenderFrameShadowCasterSubmission shadowCasterSubmission = Assert.Single(frame.ShadowCasterSubmissions);
            Assert.Same(drawable, shadowCasterSubmission.Drawable);
            Assert.Equal(0, shadowCasterSubmission.SubmeshIndex);
        }

        /// <summary>
        /// Ensures extracted frames include light submissions with stable authored light types and computed relative importance.
        /// </summary>
        [Fact]
        public void Extract_WhenVisibleLightsExist_ReturnsLightSubmissionsWithImportance() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            CameraComponent camera = new CameraComponent();
            DirectionalLightComponent shadowedDirectionalLight = new DirectionalLightComponent();
            shadowedDirectionalLight.Intensity = 2.5f;
            shadowedDirectionalLight.ShadowsEnabled = true;
            PointLightComponent pointLight = new PointLightComponent();
            pointLight.Intensity = 1.0f;
            RenderFrameExtractionService extractionService = new RenderFrameExtractionService();

            RenderFrameExtractionResult result = extractionService.Extract(
                new[] { camera },
                Array.Empty<IDrawable3D>(),
                new LightComponent[] { shadowedDirectionalLight, pointLight },
                new RendererBackendCapabilityProfile(true, false, true, true, 32, 4));

            RenderFrame frame = Assert.Single(result.Frames);
            Assert.Equal(2, frame.LightSubmissions.Count);
            Assert.Same(shadowedDirectionalLight, frame.LightSubmissions[0].Light);
            Assert.Same(pointLight, frame.LightSubmissions[1].Light);
            Assert.Equal(LightType.Directional, frame.LightSubmissions[0].LightType);
            Assert.Equal(LightType.Point, frame.LightSubmissions[1].LightType);
            Assert.True(frame.LightSubmissions[0].Importance > frame.LightSubmissions[1].Importance);
        }

        /// <summary>
        /// Minimal drawable used to drive render-frame extraction tests.
        /// </summary>
        sealed class TestDrawable3D : IDrawable3D {
            /// <summary>
            /// Initializes one test drawable with the requested material blend mode.
            /// </summary>
            /// <param name="blendMode">Blend mode assigned to the runtime material.</param>
            public TestDrawable3D(MaterialBlendMode blendMode) {
                RuntimeMaterial material = CreateMaterial(blendMode);
                Model = CreateModelWithSubmeshes(
                    new[] {
                        new RuntimeSubmesh {
                            MaterialSlotName = "Default",
                            IndexStart = 0,
                            IndexCount = 3
                        }
                    });
                Materials = new[] { material };
            }

            /// <summary>
            /// Initializes one test drawable with explicit material slots and submesh ranges.
            /// </summary>
            /// <param name="blendModes">Blend modes assigned to each runtime material slot.</param>
            /// <param name="submeshes">Runtime submeshes referenced by the drawable model.</param>
            public TestDrawable3D(MaterialBlendMode[] blendModes, RuntimeSubmesh[] submeshes) {
                if (blendModes == null) {
                    throw new ArgumentNullException(nameof(blendModes));
                } else if (submeshes == null) {
                    throw new ArgumentNullException(nameof(submeshes));
                }

                RuntimeMaterial[] materials = new RuntimeMaterial[blendModes.Length];
                for (int materialIndex = 0; materialIndex < blendModes.Length; materialIndex++) {
                    materials[materialIndex] = CreateMaterial(blendModes[materialIndex]);
                }

                Model = CreateModelWithSubmeshes(submeshes);
                Materials = materials;
            }

            /// <summary>
            /// Gets the parent entity for this drawable.
            /// </summary>
            public Entity Parent => null;

            /// <summary>
            /// Gets or sets the render order.
            /// </summary>
            public byte RenderOrder3D { get; set; }

            /// <summary>
            /// Gets the runtime model.
            /// </summary>
            public RuntimeModel Model { get; }

            /// <summary>
            /// Gets or sets the runtime materials bound to each submesh slot.
            /// </summary>
            public RuntimeMaterial[] Materials { get; set; }

            /// <summary>
            /// Creates one runtime material with the requested blend mode.
            /// </summary>
            /// <param name="blendMode">Blend mode assigned to the runtime material.</param>
            /// <returns>Configured runtime material.</returns>
            static RuntimeMaterial CreateMaterial(MaterialBlendMode blendMode) {
                RuntimeMaterial material = new RuntimeMaterial();
                material.SetRenderState(new MaterialRenderState {
                    BlendMode = blendMode
                });
                return material;
            }

            /// <summary>
            /// Creates one runtime model carrying the supplied submesh ranges.
            /// </summary>
            /// <param name="submeshes">Runtime submeshes to attach to the model.</param>
            /// <returns>Configured runtime model.</returns>
            static RuntimeModel CreateModelWithSubmeshes(RuntimeSubmesh[] submeshes) {
                if (submeshes == null) {
                    throw new ArgumentNullException(nameof(submeshes));
                }

                TestRuntimeModel model = new TestRuntimeModel();
                model.SetSubmeshes(submeshes);
                return model;
            }
        }
    }
}
