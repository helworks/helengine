using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the runtime FPS overlay component builds and updates its own text hierarchy.
    /// </summary>
    public class FPSComponentTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the runtime test harness.
        /// </summary>
        readonly string TempRootPath;
        /// <summary>
        /// Core instance configured for deterministic FPS overlay tests.
        /// </summary>
        readonly TestClockDrivenCore CoreInstance;

        /// <summary>
        /// Initializes the runtime services required by the component tests.
        /// </summary>
        public FPSComponentTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-fps-component-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            CoreInstance = new TestClockDrivenCore(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });

            CoreInstance.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Deletes temporary test content after each run.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the component creates a top-left overlay host with two text children.
        /// </summary>
        [Fact]
        public void ComponentAdded_WhenFontIsMissing_DoesNotBuildOverlayChildren() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent();

            entity.AddComponent(fps);

            Assert.Empty(entity.Children);
            Assert.Null(fps.Font);
        }

        /// <summary>
        /// Ensures update and render frame ticks refresh both visible lines.
        /// </summary>
        [Fact]
        public void CoreUpdateAndDraw_WhenFontIsMissing_DoesNotThrow() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                RefreshIntervalSeconds = 0d
            };

            entity.AddComponent(fps);

            Core.Instance.Update();
            Core.Instance.Draw();
            Core.Instance.Update();
            Core.Instance.Draw();

            Assert.Empty(entity.Children);
            Assert.Equal("Update FPS: --", fps.UpdateFpsText);
            Assert.Equal("Render FPS: -- (-- ms)", fps.RenderFpsText);
        }

        /// <summary>
        /// Ensures disabling the parent entity removes the text drawables from render participation.
        /// </summary>
        [Fact]
        public void FontProperty_WhenAssignedAfterAttachment_BuildsOverlayChildren() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent();
            entity.AddComponent(fps);

            FontAsset font = CreateFont(24f);
            fps.Font = font;

            Entity overlayHost = Assert.Single(entity.Children);
            TextComponent updateText = Assert.Single(overlayHost.Children[0].Components.OfType<TextComponent>());
            TextComponent renderText = Assert.Single(overlayHost.Children[1].Components.OfType<TextComponent>());

            Assert.Same(font, updateText.Font);
            Assert.Same(font, renderText.Font);
            Assert.Equal(font.LineHeight, overlayHost.Children[1].LocalPosition.Y);
        }

        /// <summary>
        /// Ensures a configured overlay font scale is applied to both text rows and their vertical spacing.
        /// </summary>
        [Fact]
        public void FontScale_WhenAssignedBeforeAttachment_ScalesOverlayTextAndRowSpacing() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FontAsset font = CreateFont(24f);
            FPSComponent fps = new FPSComponent {
                Font = font,
                FontScale = 2f
            };

            entity.AddComponent(fps);

            Entity overlayHost = Assert.Single(entity.Children);
            TextComponent updateText = Assert.Single(overlayHost.Children[0].Components.OfType<TextComponent>());
            TextComponent renderText = Assert.Single(overlayHost.Children[1].Components.OfType<TextComponent>());

            Assert.Equal(2f, updateText.FontScale);
            Assert.Equal(2f, renderText.FontScale);
            Assert.Equal(font.LineHeight * 2f, overlayHost.Children[1].LocalPosition.Y);
        }

        /// <summary>
        /// Ensures the Nintendo 3DS runtime halves the effective FPS row size without changing the authored component scale.
        /// </summary>
        [Fact]
        public void ThreeDsPlatform_WhenOverlayIsBuilt_HalvesEffectiveFontScaleForBothRows() {
            using Core threeDsCore = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });
            threeDsCore.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("3ds", "test-version"));

            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FontAsset font = CreateFont(24f);
            FPSComponent fps = new FPSComponent {
                Font = font,
                FontScale = 1f
            };

            entity.AddComponent(fps);

            Entity overlayHost = Assert.Single(entity.Children);
            TextComponent updateText = Assert.Single(overlayHost.Children[0].Components.OfType<TextComponent>());
            TextComponent renderText = Assert.Single(overlayHost.Children[1].Components.OfType<TextComponent>());

            Assert.Equal(1f, fps.FontScale);
            Assert.Equal(0.5f, updateText.FontScale);
            Assert.Equal(0.5f, renderText.FontScale);
            Assert.Equal(font.LineHeight * 0.5f, overlayHost.Children[1].LocalPosition.Y);
        }

        /// <summary>
        /// Ensures authored extra overlay text stays stored for compatibility without generating visible extra rows.
        /// </summary>
        [Fact]
        public void AdditionalText_WhenAssignedBeforeAttachment_DoesNotBuildVisibleAdditionalOverlayRows() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FontAsset font = CreateFont(24f);
            FPSComponent fps = new FPSComponent {
                Font = font,
                FontScale = 2f,
                AdditionalText = "Light: On (L / South Toggle)\nCamera: WASD / DPad / Stick"
            };

            entity.AddComponent(fps);

            Entity overlayHost = Assert.Single(entity.Children);
            Assert.Equal(2, overlayHost.Children.Count);
            Assert.Equal("Light: On (L / South Toggle)\nCamera: WASD / DPad / Stick", fps.AdditionalText);
        }

        /// <summary>
        /// Ensures optional authored instruction text can be absent without throwing during runtime deserialization.
        /// </summary>
        [Fact]
        public void AdditionalText_WhenAssignedNull_NormalizesToEmptyString() {
            FPSComponent fps = new FPSComponent();

            fps.AdditionalText = null;

            Assert.Equal(string.Empty, fps.AdditionalText);
        }

        /// <summary>
        /// Ensures the parameterless constructor uses the configured default font asset.
        /// </summary>
        [Fact]
        public void FontProperty_WhenClearedAfterAttachment_RemovesOverlayChildren() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont()
            };
            entity.AddComponent(fps);
            Assert.Single(entity.Children);

            fps.Font = null;

            Assert.Empty(entity.Children);
            Assert.Null(fps.Font);
            Assert.Single(Core.Instance.ObjectManager.Entities);
            Assert.Empty(Core.Instance.ObjectManager.Drawables2D);
        }

        /// <summary>
        /// Ensures removing the component disposes the generated overlay subtree instead of leaving orphaned overlay entities registered.
        /// </summary>
        [Fact]
        public void RemoveComponent_WhenOverlayWasBuilt_DisposesOverlayEntities() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont()
            };
            entity.AddComponent(fps);

            Assert.Equal(4, Core.Instance.ObjectManager.Entities.Count);
            Assert.Equal(2, Core.Instance.ObjectManager.Drawables2D.Count);

            entity.RemoveComponent(fps);

            Assert.Empty(entity.Children);
            Assert.Single(Core.Instance.ObjectManager.Entities);
            Assert.Empty(Core.Instance.ObjectManager.Drawables2D);
        }

        /// <summary>
        /// Ensures disposing the owning entity removes the component from the global FPS sampling list.
        /// </summary>
        [Fact]
        public void Dispose_WhenOwningEntityIsDisposed_RemovesComponentFromActiveSamplingList() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont()
            };
            entity.AddComponent(fps);

            Assert.Contains(fps, GetActiveComponents());

            entity.Dispose();

            Assert.DoesNotContain(fps, GetActiveComponents());
        }

        /// <summary>
        /// Ensures externally disposed overlay entities become invalid references instead of requiring a hidden cleanup sentinel.
        /// </summary>
        [Fact]
        public void OverlayHost_WhenDisposedExternally_ThrowsInvalidOperationExceptionOnLaterAccess() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont()
            };

            entity.AddComponent(fps);

            Entity overlayHost = Assert.IsType<Entity>(GetPrivateFieldValue(fps, "OverlayHost"));
            overlayHost.Dispose();

            Assert.Throws<InvalidOperationException>(() => _ = overlayHost.Position);
        }

        /// <summary>
        /// Ensures both FPS lines format sampled values with exactly one decimal place.
        /// </summary>
        [Fact]
        public void CoreUpdateAndDraw_WhenSamplingFrames_FormatsFpsWithOneDecimalPlace() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont(),
                RefreshIntervalSeconds = 0d
            };

            entity.AddComponent(fps);

            Core.Instance.Update();
            Core.Instance.Draw();
            Core.Instance.Update();
            Core.Instance.Draw();

            Assert.Matches("^Update FPS: [0-9]+\\.[0-9]$", fps.UpdateFpsText);
            Assert.Matches("^Render FPS: [0-9]+\\.[0-9] \\([0-9]+\\.[0-9] ms\\)$", fps.RenderFpsText);
        }

        /// <summary>
        /// Ensures explicit core frame timing, not wall-clock sampling, drives the visible FPS values and draw duration text.
        /// </summary>
        [Fact]
        public void CoreUpdateAndDraw_WhenExplicitElapsedSecondsAreUsed_UsesCoreTimingForFpsSamplingAndRenderDrawMilliseconds() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont(),
                RefreshIntervalSeconds = 0d
            };

            entity.AddComponent(fps);
            CoreInstance.QueueMeasuredDrawMilliseconds(new[] { 12.3d });

            Core.Instance.Update(0.25d);
            Core.Instance.Draw();
            Core.Instance.Update(0.25d);

            Assert.Equal("Update FPS: 4.0", fps.UpdateFpsText);
            Assert.Equal("Render FPS: 4.0 (12.3 ms)", fps.RenderFpsText);
        }

        /// <summary>
        /// Ensures the FPS component prefers core-owned overlay metrics when the active runtime publishes custom diagnostics.
        /// </summary>
        [Fact]
        public void CoreUpdateAndDraw_WhenRendererProvidesOverlayRows_UsesRendererOwnedOverlayText() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont(),
                RefreshIntervalSeconds = 0d
            };

            entity.AddComponent(fps);
            Core.Instance.SetPerformanceOverlayMetrics(true, 7.0d, 1.5d, 0.5d, 8.0d, 0.8d, 0.2d, 12, 3);

            Core.Instance.Update(0.25d);
            Core.Instance.Draw();
            Core.Instance.Update(0.25d);

            Assert.Equal("Update FPS: 4.0 Set 7.0 Prep 1.5 Emit 0.5", fps.UpdateFpsText);
            Assert.Equal("Render FPS: 4.0 Drw 0.0 Enc 8.0 Lgt 0.8", fps.RenderFpsText);
            Assert.Equal(string.Empty, fps.DetailFpsText);
        }

        /// <summary>
        /// Ensures platform-owned overlays never fall back to generic triangle timing suffixes.
        /// </summary>
        [Fact]
        public void CoreUpdateAndDraw_WhenPlatformOwnsOverlayPresentation_HidesGenericPerformanceRows() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont(),
                RefreshIntervalSeconds = 0d
            };

            entity.AddComponent(fps);
            Core.Instance.SetPlatformOwnedPerformanceOverlayPresentation(true);
            Core.Instance.SetPerformanceOverlayMetrics(true, 7.0d, 1.5d, 0.5d, 8.0d, 0.8d, 0.2d, 12, 3);

            Core.Instance.Update(0.25d);
            Core.Instance.Draw();
            Core.Instance.Update(0.25d);

            Assert.DoesNotContain("Set", fps.UpdateFpsText, StringComparison.Ordinal);
            Assert.DoesNotContain("Prep", fps.UpdateFpsText, StringComparison.Ordinal);
            Assert.DoesNotContain("Emit", fps.UpdateFpsText, StringComparison.Ordinal);
            Assert.DoesNotContain("Enc", fps.RenderFpsText, StringComparison.Ordinal);
            Assert.DoesNotContain("Lgt", fps.RenderFpsText, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures platform-published overlay text rows only replace the two visible summary rows.
        /// </summary>
        [Fact]
        public void CoreUpdateAndDraw_WhenPlatformPublishesOverlayTextRows_UsesPublishedTextAndAdditionalRows() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont(),
                RefreshIntervalSeconds = 0d
            };

            entity.AddComponent(fps);
            Core.Instance.SetPerformanceOverlayMetrics(true, 7.0d, 1.5d, 0.5d, 8.0d, 0.8d, 0.2d, 12, 3);
            Core.Instance.SetPerformanceOverlayTextRows(
                true,
                "Upd 4.0 Q3D 1 Sub 1",
                "Rdr 4.0 Drw 15.8 2D 0.4",
                "Set 0.1 Geo 0.2 Fl 15.1",
                "Xf 0.0 Mat 0.0 DL 0.1\nPre 0.0 Kck 0.1 Pst 0.0");

            Core.Instance.Update(0.25d);
            Core.Instance.Draw();
            Core.Instance.Update(0.25d);

            Assert.Equal("Upd 4.0 Q3D 1 Sub 1", fps.UpdateFpsText);
            Assert.Equal("Rdr 4.0 Drw 15.8 2D 0.4", fps.RenderFpsText);
            Assert.Equal(string.Empty, fps.DetailFpsText);

            Entity overlayHost = Assert.Single(entity.Children);
            Assert.Equal(2, overlayHost.Children.Count);
        }

        /// <summary>
        /// Ensures platform detail-only rows are ignored while the FPS component keeps the compact two-line summary.
        /// </summary>
        [Fact]
        public void CoreUpdateAndDraw_WhenPlatformPublishesOnlyDetailRows_KeepsCompactUpdateAndRenderSummaryRows() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont(),
                RefreshIntervalSeconds = 0d
            };

            entity.AddComponent(fps);
            CoreInstance.QueueMeasuredDrawMilliseconds(new[] { 15.8d });
            Core.Instance.SetPerformanceOverlayMetrics(true, 7.0d, 1.5d, 0.5d, 8.0d, 0.8d, 0.2d, 12, 3);
            Core.Instance.SetPerformanceOverlayTextRows(
                true,
                string.Empty,
                string.Empty,
                "Q3D 1 Sub 1 2D 0.4",
                "Set 0.1 Geo 0.2 Fl 15.1\nDL 0.1 Pre 0.0 K 0.1 P 0.0");

            Core.Instance.Update(0.25d);
            Core.Instance.Draw();
            Core.Instance.Update(0.25d);

            Assert.Equal("Update FPS: 4.0", fps.UpdateFpsText);
            Assert.Equal("Render FPS: 4.0 Drw 15.8", fps.RenderFpsText);
            Assert.Equal(string.Empty, fps.DetailFpsText);

            Entity overlayHost = Assert.Single(entity.Children);
            Assert.Equal(2, overlayHost.Children.Count);
        }

        /// <summary>
        /// Ensures one platform-owned presentation path can consume the resolved overlay rows while the scene-owned overlay hierarchy stays disabled.
        /// </summary>
        [Fact]
        public void CoreUpdateAndDraw_WhenPlatformOwnsOverlayPresentation_PublishesResolvedRowsAndDisablesSceneHierarchy() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont(),
                RefreshIntervalSeconds = 0d
            };

            Core.Instance.SetPlatformOwnedPerformanceOverlayPresentation(true);
            Core.Instance.SetPerformanceOverlayMetrics(true, 7.0d, 1.5d, 0.5d, 8.0d, 0.8d, 0.2d, 12, 3);
            Core.Instance.SetPerformanceOverlayTextRows(
                true,
                string.Empty,
                string.Empty,
                "Q3D 1 Sub 1 2D 0.4",
                "Set 0.1 Geo 0.2 Fl 15.1\nDL 0.1 Pre 0.0 K 0.1 P 0.0");
            CoreInstance.QueueMeasuredDrawMilliseconds(new[] { 15.8d });

            entity.AddComponent(fps);

            Core.Instance.Update(0.25d);
            Core.Instance.Draw();
            Core.Instance.Update(0.25d);

            Entity overlayHost = Assert.Single(entity.Children);
            Assert.False(overlayHost.Enabled);
            Assert.Equal("Update FPS: 4.0", Core.Instance.ResolvedPerformanceOverlayUpdateText);
            Assert.Equal("Render FPS: 4.0 Drw 15.8", Core.Instance.ResolvedPerformanceOverlayRenderText);
            Assert.Equal(string.Empty, Core.Instance.ResolvedPerformanceOverlayDetailText);
            Assert.Equal(string.Empty, Core.Instance.ResolvedPerformanceOverlayAdditionalText);
            Assert.Same(fps.Font, Core.Instance.ResolvedPerformanceOverlayFont);
            Assert.Equal(fps.Padding.X, Core.Instance.ResolvedPerformanceOverlayPadding.X);
            Assert.Equal(fps.Padding.Y, Core.Instance.ResolvedPerformanceOverlayPadding.Y);
        }

        /// <summary>
        /// Ensures the built overlay resolves core-owned metrics immediately instead of leaving placeholders visible until the first sample window completes.
        /// </summary>
        [Fact]
        public void ComponentAdded_WhenRendererProvidesOverlayRows_UsesRendererOwnedTextImmediatelyAfterOverlayBuild() {
            Core.Instance.SetPerformanceOverlayMetrics(true, 0d, 0d, 0d, 0d, 0d, 0d, 0, 0);

            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont()
            };

            entity.AddComponent(fps);

            Assert.Equal("Update FPS: 0.0 Set 0.0 Prep 0.0 Emit 0.0", fps.UpdateFpsText);
            Assert.Equal("Render FPS: 0.0 Drw 0.0 Enc 0.0 Lgt 0.0", fps.RenderFpsText);
            Assert.Equal(string.Empty, fps.DetailFpsText);
        }

        /// <summary>
        /// Ensures the overlay uses the default FPS rows until the active renderer explicitly publishes performance-overlay metrics.
        /// </summary>
        [Fact]
        public void ComponentAdded_WhenRendererHasNotPublishedOverlayMetrics_UsesDefaultOverlayText() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont()
            };

            entity.AddComponent(fps);

            Assert.Equal("Update FPS: 0.0", fps.UpdateFpsText);
            Assert.Equal("Render FPS: 0.0 (0.0 ms)", fps.RenderFpsText);
            Assert.Equal(string.Empty, fps.DetailFpsText);
        }

        /// <summary>
        /// Ensures the FPS overlay metrics live on the core contract instead of relying on renderer virtual dispatch.
        /// </summary>
        [Fact]
        public void PerformanceOverlayContract_UsesCoreOwnedMetricsInsteadOfRendererVirtuals() {
            Assert.Null(typeof(RenderManager3D).GetMethod("UsesPerformanceOverlayMetrics"));
            Assert.Null(typeof(RenderManager3D).GetMethod("GetPerformanceOverlayTriangleSetupMilliseconds"));
            Assert.Null(typeof(RenderManager3D).GetMethod("GetPerformanceOverlayDispatchCount"));
            Assert.NotNull(typeof(Core).GetProperty("UsesPerformanceOverlayMetrics"));
            Assert.NotNull(typeof(Core).GetMethod("SetPerformanceOverlayMetrics"));
            Assert.NotNull(typeof(Core).GetMethod("SetPerformanceOverlayTextRows"));
        }

        /// <summary>
        /// Creates a deterministic font asset containing the glyphs needed by the overlay labels.
        /// </summary>
        /// <returns>Font asset with stable glyph metrics for the tests.</returns>
        FontAsset CreateFont() {
            return CreateFont(16f);
        }

        /// <summary>
        /// Creates a deterministic font asset containing the glyphs needed by the overlay labels.
        /// </summary>
        /// <param name="lineHeight">Line height assigned to the generated test font.</param>
        /// <returns>Font asset with stable glyph metrics for the tests.</returns>
        FontAsset CreateFont(float lineHeight) {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['U'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['R'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                [':'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                [' '] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['('] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                [')'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['-'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
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
                ['.'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                lineHeight,
                64,
                64);
        }

        /// <summary>
        /// Reads one private instance field value from the supplied test subject.
        /// </summary>
        /// <param name="target">Object that owns the requested field.</param>
        /// <param name="fieldName">Exact private field name to read.</param>
        /// <returns>Current field value.</returns>
        static object GetPrivateFieldValue(object target, string fieldName) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            } else if (string.IsNullOrWhiteSpace(fieldName)) {
                throw new ArgumentException("Field name must be provided.", nameof(fieldName));
            }

            Type currentType = target.GetType();
            while (currentType != null) {
                var field = currentType.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null) {
                    return field.GetValue(target);
                }

                currentType = currentType.BaseType;
            }

            throw new InvalidOperationException($"Field '{fieldName}' was not found on type '{target.GetType().FullName}'.");
        }

        /// <summary>
        /// Reads the static active FPS component list through reflection so disposal behavior can be verified directly.
        /// </summary>
        /// <returns>Snapshot of the currently registered active FPS components.</returns>
        static IReadOnlyList<FPSComponent> GetActiveComponents() {
            var field = typeof(FPSComponent).GetField("ActiveComponents", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field == null) {
                throw new InvalidOperationException("FPSComponent.ActiveComponents field was not found.");
            }

            return Assert.IsAssignableFrom<IReadOnlyList<FPSComponent>>(field.GetValue(null));
        }

    }
}


