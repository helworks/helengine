using helengine.editor.tests.testing;

namespace helengine.editor.tests.components {
    /// <summary>
    /// Verifies reference-canvas fit behavior scales one authored 2D hierarchy into smaller desktop windows without losing anchor semantics.
    /// </summary>
    public sealed class ReferenceCanvasFitComponentTests : IDisposable {
        /// <summary>
        /// Initializes the core services required by the fit-component tests.
        /// </summary>
        public ReferenceCanvasFitComponentTests() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend());
        }

        /// <summary>
        /// Releases the active core instance after each test.
        /// </summary>
        public void Dispose() {
            Core.Instance?.Dispose();
        }

        /// <summary>
        /// Ensures one authored menu subtree scales positions, sizes, anchors, and text glyph scale uniformly when the live window shrinks from 1280x720 to 640x480.
        /// </summary>
        [Fact]
        public void ComponentAdded_WhenWindowShrinksTo640x480_ScalesTheAuthoredSubtreeUniformly() {
            TestRenderManager3D renderManager = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            renderManager.OnWindowResize(IntPtr.Zero, 1280, 720);

            Entity menuRoot = CreateEntity(float3.Zero);
            menuRoot.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.ScreenBindingMode,
                FixedSize = new int2(1280, 720)
            });

            Entity generatedRoot = CreateEntity(float3.Zero);
            menuRoot.AddChild(generatedRoot);

            Entity panelEntity = CreateEntity(new float3(88f, 190f, 0f));
            RoundedRectComponent panelBackground = new RoundedRectComponent {
                Size = new int2(560, 420),
                Radius = 18f,
                BorderThickness = 3f
            };
            AnchorComponent panelAnchor = new AnchorComponent();
            panelEntity.AddComponent(panelBackground);
            panelEntity.AddComponent(panelAnchor);
            generatedRoot.AddChild(panelEntity);
            panelAnchor.SetAnchorDistances(left: 88f, top: 190f);

            Entity headingEntity = CreateEntity(new float3(32f, 30f, 0.1f));
            TextComponent headingText = new TextComponent {
                Font = CreateFont(),
                Text = "Heading",
                Size = new int2(420, 36),
                FontScale = 1f
            };
            headingEntity.AddComponent(headingText);
            panelEntity.AddChild(headingEntity);

            Entity viewportEntity = CreateEntity(new float3(32f, 90f, 0f));
            ClipRectComponent clipRectComponent = new ClipRectComponent {
                Size = new int2(496, 272)
            };
            viewportEntity.AddComponent(clipRectComponent);
            panelEntity.AddChild(viewportEntity);

            Entity itemsRootEntity = CreateEntity(float3.Zero);
            ScrollComponent scrollComponent = new ScrollComponent {
                Size = new int2(496, 272),
                ItemCount = 6,
                VisibleItemCount = 4,
                ItemExtent = 56
            };
            itemsRootEntity.AddComponent(scrollComponent);
            viewportEntity.AddChild(itemsRootEntity);

            menuRoot.AddComponent(new ReferenceCanvasFitComponent {
                ReferenceWidth = 1280,
                ReferenceHeight = 720
            });

            renderManager.OnWindowResize(IntPtr.Zero, 640, 480);
            Core.Instance.Update();

            Assert.Equal(new float3(44f, 95f, 0f), panelEntity.LocalPosition);
            Assert.Equal(new int2(280, 210), panelBackground.Size);
            Assert.Equal(9f, panelBackground.Radius);
            Assert.Equal(1.5f, panelBackground.BorderThickness);
            Assert.Equal(new float4(44f, 0f, 95f, 0f), panelAnchor.AnchorDistances);
            Assert.Equal(new float3(16f, 15f, 0.1f), headingEntity.LocalPosition);
            Assert.Equal(new int2(210, 18), headingText.Size);
            Assert.Equal(0.5f, headingText.FontScale);
            Assert.Equal(new int2(248, 136), clipRectComponent.Size);
            Assert.Equal(new int2(248, 136), scrollComponent.Size);
            Assert.Equal(28, scrollComponent.ItemExtent);
        }

        /// <summary>
        /// Ensures one authored interactable region scales with the fitted visual subtree so pointer hit targets remain aligned with the rendered control.
        /// </summary>
        [Fact]
        public void ComponentAdded_WhenWindowShrinksTo640x480_ScalesInteractableBounds() {
            TestRenderManager3D renderManager = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            renderManager.OnWindowResize(IntPtr.Zero, 1280, 720);

            Entity menuRoot = CreateEntity(float3.Zero);
            menuRoot.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.ScreenBindingMode,
                FixedSize = new int2(1280, 720)
            });

            Entity generatedRoot = CreateEntity(float3.Zero);
            menuRoot.AddChild(generatedRoot);

            Entity buttonEntity = CreateEntity(new float3(160f, 240f, 0f));
            RoundedRectComponent background = new RoundedRectComponent {
                Size = new int2(400, 120),
                Radius = 12f,
                BorderThickness = 2f
            };
            InteractableComponent interactable = new InteractableComponent {
                Size = new int2(400, 120)
            };
            buttonEntity.AddComponent(background);
            buttonEntity.AddComponent(interactable);
            generatedRoot.AddChild(buttonEntity);

            menuRoot.AddComponent(new ReferenceCanvasFitComponent {
                ReferenceWidth = 1280,
                ReferenceHeight = 720
            });

            renderManager.OnWindowResize(IntPtr.Zero, 640, 480);
            Core.Instance.Update();

            Assert.Equal(new float3(80f, 120f, 0f), buttonEntity.LocalPosition);
            Assert.Equal(new int2(200, 60), background.Size);
            Assert.Equal(new int2(200, 60), interactable.Size);
        }

        /// <summary>
        /// Ensures same-aspect widescreen resolutions preserve the same normalized menu layout instead of drifting when the window shrinks.
        /// </summary>
        [Fact]
        public void ComponentAdded_WhenWindowShrinksTo853x480_PreservesNormalizedLayoutFrom1280x720() {
            TestRenderManager3D renderManager = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            renderManager.OnWindowResize(IntPtr.Zero, 1280, 720);

            Entity menuRoot = CreateEntity(float3.Zero);
            menuRoot.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.ScreenBindingMode,
                FixedSize = new int2(1280, 720)
            });

            Entity generatedRoot = CreateEntity(float3.Zero);
            menuRoot.AddChild(generatedRoot);

            Entity panelEntity = CreateEntity(new float3(88f, 190f, 0f));
            RoundedRectComponent panelBackground = new RoundedRectComponent {
                Size = new int2(560, 420),
                Radius = 18f,
                BorderThickness = 3f
            };
            AnchorComponent panelAnchor = new AnchorComponent();
            panelEntity.AddComponent(panelBackground);
            panelEntity.AddComponent(panelAnchor);
            generatedRoot.AddChild(panelEntity);
            panelAnchor.SetAnchorDistances(left: 88f, top: 190f);

            menuRoot.AddComponent(new ReferenceCanvasFitComponent {
                ReferenceWidth = 1280,
                ReferenceHeight = 720
            });

            renderManager.OnWindowResize(IntPtr.Zero, 853, 480);
            Core.Instance.Update();

            AssertFloat3ApproximatelyEqual(new float3(58.64375f, 126.66667f, 0f), panelEntity.LocalPosition, 0.01f);
            Assert.Equal(new int2(373, 280), panelBackground.Size);
            AssertFloat4ApproximatelyEqual(new float4(58.64375f, 0f, 126.66667f, 0f), panelAnchor.AnchorDistances, 0.01f);
        }

        /// <summary>
        /// Ensures same-aspect widescreen shrink targets keep the authored panel fully inside the visible fitted canvas.
        /// </summary>
        [Fact]
        public void ComponentAdded_WhenWindowIs853x480_KeepsPanelInsideTheVisibleCanvas() {
            TestRenderManager3D renderManager = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            renderManager.OnWindowResize(IntPtr.Zero, 1280, 720);

            Entity menuRoot = CreateEntity(float3.Zero);
            menuRoot.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.ScreenBindingMode,
                FixedSize = new int2(1280, 720)
            });

            Entity generatedRoot = CreateEntity(float3.Zero);
            menuRoot.AddChild(generatedRoot);

            Entity panelEntity = CreateEntity(new float3(88f, 190f, 0f));
            RoundedRectComponent panelBackground = new RoundedRectComponent {
                Size = new int2(560, 420),
                Radius = 18f,
                BorderThickness = 3f
            };
            AnchorComponent panelAnchor = new AnchorComponent();
            panelEntity.AddComponent(panelBackground);
            panelEntity.AddComponent(panelAnchor);
            generatedRoot.AddChild(panelEntity);
            panelAnchor.SetAnchorDistances(left: 88f, top: 190f);

            Entity descriptionEntity = CreateEntity(new float3(32f, 410f, 0.1f));
            TextComponent descriptionText = new TextComponent {
                Font = CreateFont(),
                Text = "Description",
                Size = new int2(500, 64),
                FontScale = 1f
            };
            descriptionEntity.AddComponent(descriptionText);
            panelEntity.AddChild(descriptionEntity);

            menuRoot.AddComponent(new ReferenceCanvasFitComponent {
                ReferenceWidth = 1280,
                ReferenceHeight = 720
            });

            renderManager.OnWindowResize(IntPtr.Zero, 853, 480);
            Core.Instance.Update();

            float panelRight = panelEntity.LocalPosition.X + panelBackground.Size.X;
            float panelBottom = panelEntity.LocalPosition.Y + panelBackground.Size.Y;
            float descriptionBottom = panelEntity.LocalPosition.Y + descriptionEntity.LocalPosition.Y + descriptionText.Size.Y;

            Assert.True(panelEntity.LocalPosition.X >= 0f);
            Assert.True(panelEntity.LocalPosition.Y >= 0f);
            Assert.True(panelRight <= 853f);
            Assert.True(panelBottom <= 480f);
            Assert.True(descriptionBottom <= 480f);
        }

        /// <summary>
        /// Creates one initialized entity with the supplied local position.
        /// </summary>
        /// <param name="localPosition">Local position assigned to the entity.</param>
        /// <returns>Initialized entity ready to receive hierarchy content.</returns>
        static Entity CreateEntity(float3 localPosition) {
            Entity entity = new Entity {
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity
            };
            entity.InitComponents();
            entity.InitChildren();
            return entity;
        }

        /// <summary>
        /// Creates one deterministic font asset for text scaling assertions.
        /// </summary>
        /// <returns>Font asset with fixed glyph metrics.</returns>
        static FontAsset CreateFont() {
            TestRuntimeTexture texture = new TestRuntimeTexture {
                Width = 100,
                Height = 50
            };
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['H'] = new FontChar(new float4(0.1f, 0.2f, 0.05f, 0.1f), 1f, 6f, 0f, 0f)
            };
            return new FontAsset(new FontInfo("Test", 10, 3f), texture, characters, 10f, 100, 50);
        }

        /// <summary>
        /// Asserts that two three-component floating-point vectors are equal within a caller-supplied tolerance.
        /// </summary>
        /// <param name="expected">Expected vector value.</param>
        /// <param name="actual">Actual vector value.</param>
        /// <param name="tolerance">Maximum absolute difference allowed for each component.</param>
        static void AssertFloat3ApproximatelyEqual(float3 expected, float3 actual, float tolerance) {
            AssertApproximatelyEqual(expected.X, actual.X, tolerance);
            AssertApproximatelyEqual(expected.Y, actual.Y, tolerance);
            AssertApproximatelyEqual(expected.Z, actual.Z, tolerance);
        }

        /// <summary>
        /// Asserts that two four-component floating-point vectors are equal within a caller-supplied tolerance.
        /// </summary>
        /// <param name="expected">Expected vector value.</param>
        /// <param name="actual">Actual vector value.</param>
        /// <param name="tolerance">Maximum absolute difference allowed for each component.</param>
        static void AssertFloat4ApproximatelyEqual(float4 expected, float4 actual, float tolerance) {
            AssertApproximatelyEqual(expected.X, actual.X, tolerance);
            AssertApproximatelyEqual(expected.Y, actual.Y, tolerance);
            AssertApproximatelyEqual(expected.Z, actual.Z, tolerance);
            AssertApproximatelyEqual(expected.W, actual.W, tolerance);
        }

        /// <summary>
        /// Asserts that two floating-point values are equal within a caller-supplied tolerance.
        /// </summary>
        /// <param name="expected">Expected floating-point value.</param>
        /// <param name="actual">Actual floating-point value.</param>
        /// <param name="tolerance">Maximum absolute difference allowed between the two values.</param>
        static void AssertApproximatelyEqual(float expected, float actual, float tolerance) {
            float difference = Math.Abs(expected - actual);
            Assert.True(
                difference <= tolerance,
                $"Expected {expected} but received {actual}. Difference {difference} exceeded tolerance {tolerance}.");
        }
    }
}
