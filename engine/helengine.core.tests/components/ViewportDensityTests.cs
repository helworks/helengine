using helengine;
using helengine.core.tests.managers.rendering;

namespace helengine.core.tests.components {
    /// <summary>Exercises shared viewport density scaling without an Android renderer or device.</summary>
    public sealed class ViewportDensityTests {
        /// <summary>Physical density changes restore authored sizes, font effects and pointer bounds without accumulating scale; resolution changes leave density sizes stable.</summary>
        [Fact]
        public void DensityChangesScaleAuthoredLayoutWithoutChangingRenderResolution() {
            using MultiWindowTestRenderManager meshes = new MultiWindowTestRenderManager();
            using Batch2DTestRenderManager overlay = new Batch2DTestRenderManager();
            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(meshes, overlay, null, new PlatformInfo("test", "test-version"));
            meshes.AddWindow(IntPtr.Zero, 1920, 1080);
            meshes.SetWindowDensityDpi(IntPtr.Zero, 254);
            Entity root = new Entity(core);
            root.InitComponents();
            root.InitChildren();
            TextComponent text = new TextComponent { Size = new int2(100, 20), FontScale = 1f, OutlineScale = 1f };
            InteractableComponent hit = new InteractableComponent { Size = new int2(100, 20) };
            root.AddComponent(text);
            root.AddComponent(hit);
            ViewportComponent viewport = new ViewportComponent {
                ScalingMode = ViewportComponent.DensityScalingMode,
                PixelsPerCentimeter = 50f
            };
            root.AddComponent(viewport);
            viewport.Update();
            Assert.Equal(2f, viewport.DensityScale);
            Assert.Equal(new int2(200, 40), text.Size);
            Assert.Equal(new int2(200, 40), hit.Size);
            Assert.Equal(2f, text.FontScale);
            Assert.Equal(2f, text.OutlineScale);
            Assert.Equal(new int2(1920, 1080), meshes.InputWindowSize);
            Entity added = new Entity(core);
            added.InitComponents();
            TextComponent addedText = new TextComponent { Size = new int2(40, 10), FontScale = 1f };
            added.AddComponent(addedText);
            root.AddChild(added);
            viewport.Update();
            Assert.Equal(new int2(200, 40), text.Size);
            Assert.Equal(new int2(80, 20), addedText.Size);
            meshes.OnWindowResize(IntPtr.Zero, 1280, 720);
            viewport.Update();
            Assert.Equal(new int2(200, 40), text.Size);
            Assert.Equal(new int2(1280, 720), viewport.AnchorSpace.Size);
            meshes.SetWindowDensityDpi(IntPtr.Zero, 127);
            viewport.Update();
            Assert.Equal(new int2(100, 20), text.Size);
            Assert.Equal(new int2(100, 20), hit.Size);
            Assert.Equal(1f, text.FontScale);
            viewport.UiScale = 1.5f;
            viewport.Update();
            viewport.Update();
            Assert.Equal(new int2(150, 30), text.Size);
        }

        /// <summary>A camera-bound viewport follows its own display density and ignores changes on the primary window.</summary>
        [Fact]
        public void CameraDensityFollowsItsNamedOutput() {
            using MultiWindowTestRenderManager meshes = new MultiWindowTestRenderManager();
            using Batch2DTestRenderManager overlay = new Batch2DTestRenderManager();
            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(meshes, overlay, null, new PlatformInfo("test", "test-version"));
            meshes.AddWindow(IntPtr.Zero, 1920, 1080);
            meshes.AddWindow(new IntPtr(1), 1280, 720);
            meshes.SetWindowTag(new IntPtr(1), "preview");
            meshes.SetWindowDensityDpi(IntPtr.Zero, 127);
            meshes.SetWindowDensityDpi(new IntPtr(1), 254);
            Entity root = new Entity(core);
            root.InitComponents();
            TextComponent text = new TextComponent { Size = new int2(100, 20) };
            root.AddComponent(text);
            ViewportComponent viewport = new ViewportComponent {
                BindingMode = ViewportComponent.ExplicitCameraBindingMode,
                BoundCameraComponent = new CameraComponent { OutputWindowTag = "preview" },
                ScalingMode = ViewportComponent.DensityScalingMode,
                PixelsPerCentimeter = 50f
            };
            root.AddComponent(viewport);
            viewport.Update();
            Assert.Equal(new int2(200, 40), text.Size);
            meshes.SetWindowDensityDpi(IntPtr.Zero, 381);
            viewport.Update();
            Assert.Equal(new int2(200, 40), text.Size);
            meshes.SetWindowDensityDpi(new IntPtr(1), 127);
            viewport.Update();
            Assert.Equal(new int2(100, 20), text.Size);
        }

        /// <summary>Authored physical conversion settings reject zero, negative and non-finite measurements.</summary>
        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void DensitySettingsRequirePositiveFiniteValues(float value) {
            ViewportComponent viewport = new ViewportComponent();
            Assert.Throws<ArgumentOutOfRangeException>(() => viewport.PixelsPerCentimeter = value);
            Assert.Throws<ArgumentOutOfRangeException>(() => viewport.UiScale = value);
        }
    }
}
