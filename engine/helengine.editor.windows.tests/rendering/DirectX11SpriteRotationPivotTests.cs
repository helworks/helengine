using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using helengine.directx11;
using SharpDX.Direct3D11;

namespace helengine.editor.windows.tests.rendering {
    /// <summary>
    /// Renders rotated sprites through the DirectX11 2D renderer into an offscreen target and compares the covered
    /// pixels with the PS2 hardware corner geometry: the sprite rotates around its transformed top-left origin, with
    /// right = (cos r, sin r) and down = (-sin r, cos r) in Y-down screen space.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class DirectX11SpriteRotationPivotTests {
        /// <summary>Side length of the square offscreen target in pixels.</summary>
        const int TargetSize = 64;

        /// <summary>Unscaled sprite width; wider than tall so a quarter turn is distinguishable from no turn.</summary>
        const int SpriteWidth = 24;

        /// <summary>Unscaled sprite height in pixels.</summary>
        const int SpriteHeight = 8;

        /// <summary>Distance from a quad edge or the colour split inside which pixels are not classified.</summary>
        const double EdgeTolerance = 1.0;

        /// <summary>
        /// A parent rotated around Z carries a child sprite offset by minus half its size, like the demodisc menu
        /// background. The sprite must cover the PS2 corner quad, which rotates in place around the parent.
        /// </summary>
        /// <param name="degrees">Rotation applied to the parent entity around the Z axis.</param>
        /// <param name="parentX">Parent X position in target pixels.</param>
        /// <param name="parentY">Parent Y position in target pixels.</param>
        [Theory]
        [InlineData(90.0, 32f, 32f)]
        [InlineData(180.0, 32f, 32f)]
        [InlineData(-90.0, 30f, 34f)]
        [InlineData(30.0, 32f, 32f)]
        public void RotatedChildSprite_CoversPs2CornerGeometry(double degrees, float parentX, float parentY) {
            Exception failure = null;
            Thread thread = new Thread(() => {
                try {
                    AssertRotatedSpriteCoverage(degrees, parentX, parentY);
                } catch (Exception exception) {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        [Theory]
        [InlineData(30d, 1.5f, 0.75f)]
        [InlineData(90d, 1f, 1f)]
        [InlineData(-45d, -1.25f, 0.75f)]
        public void ScalarRotationAndCenterPivot_PreserveParentAffineGeometry(double degrees, float scaleX, float scaleY) {
            Exception failure = null;
            Thread thread = new Thread(() => {
                try { AssertRotatedSpriteCoverage(degrees, 32f, 32f, true, scaleX, scaleY); }
                catch (Exception exception) { failure = exception; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start(); thread.Join();
            Assert.Null(failure);
        }

        /// <summary>
        /// Draws a two-colour sprite (left half red, right half blue) under a rotated parent and checks every pixel
        /// of the target against the PS2 corner quad, so both the pivot and the rotation direction are verified.
        /// </summary>
        /// <param name="degrees">Parent rotation around Z in degrees.</param>
        /// <param name="parentX">Parent X position in target pixels.</param>
        /// <param name="parentY">Parent Y position in target pixels.</param>
        static void AssertRotatedSpriteCoverage(double degrees, float parentX, float parentY, bool scalar = false, float scaleX = 1f, float scaleY = 1f) {
            using DirectX11Renderer3D renderer = new DirectX11Renderer3D();
            using Form window = new Form {
                ClientSize = new Size(TargetSize, TargetSize), ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000)
            };
            window.Show();
            Application.DoEvents();
            renderer.AddWindow(window.Handle, TargetSize, TargetSize);
            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(renderer, renderer.Render2D, null, new PlatformInfo("windows", "test"));
            using DirectX11RenderTargetResource target = Assert.IsType<DirectX11RenderTargetResource>(
                renderer.CreateRenderTarget(TargetSize, TargetSize));
            Entity cameraEntity = CreateEntity(core);
            cameraEntity.AddComponent(new CameraComponent {
                LayerMask = 1, RenderTarget = target, Viewport = new float4(0, 0, TargetSize, TargetSize),
                ClearSettings = new CameraClearSettings(true, new float4(0, 0, 0, 1), true, 1, false, 0)
            });
            RuntimeTexture halves = renderer.Render2D.BuildTextureFromRaw(new TextureAsset {
                Width = 2, Height = 1, Colors = [255, 0, 0, 255, 0, 0, 255, 255]
            });

            Entity pivot = CreateEntity(core);
            pivot.LocalPosition = new float3(parentX, parentY, 0);
            float3 zAxis = new float3(0f, 0f, 1f);
            float4.CreateFromAxisAngle(ref zAxis, (float)(degrees * Math.PI / 180.0), out float4 orientation);
            pivot.LocalOrientation = scalar ? float4.Identity : orientation;
            pivot.LocalScale = new float3(scaleX, scaleY, 1f);
            Entity spriteEntity = CreateEntity(core);
            pivot.AddChild(spriteEntity);
            spriteEntity.LocalPosition = scalar ? float3.Zero : new float3(-SpriteWidth * 0.5f, -SpriteHeight * 0.5f, 0);
            spriteEntity.AddComponent(new SpriteComponent {
                Texture = halves, Size = new int2(SpriteWidth, SpriteHeight),
                Pivot = scalar ? new float2(0.5f, 0.5f) : new float2(0f, 0f),
                RotationRadians = scalar ? (float)(degrees * Math.PI / 180d) : 0f
            });

            renderer.Draw();

            float3 origin = spriteEntity.Position;
            float4x4 world = spriteEntity.WorldTransformMatrix;
            double angle = scalar ? degrees * Math.PI / 180d : 0d;
            double cosine = Math.Cos(angle), sine = Math.Sin(angle);
            double rightX = cosine * world.M11 + sine * world.M21;
            double rightY = cosine * world.M12 + sine * world.M22;
            double downX = -sine * world.M11 + cosine * world.M21;
            double downY = -sine * world.M12 + cosine * world.M22;
            if (scalar) {
                origin.X -= (float)(rightX * SpriteWidth * 0.5 + downX * SpriteHeight * 0.5);
                origin.Y -= (float)(rightY * SpriteWidth * 0.5 + downY * SpriteHeight * 0.5);
            }
            double determinant = rightX * downY - rightY * downX;
            byte4[,] pixels = ReadPixels(target, renderer.Device);
            byte4 red = new byte4(255, 0, 0, 255);
            byte4 blue = new byte4(0, 0, 255, 255);
            byte4 background = new byte4(0, 0, 0, 255);
            int classified = 0;
            int insideCount = 0;
            List<string> mismatches = new List<string>();
            for (int y = 0; y < TargetSize; y++) {
                for (int x = 0; x < TargetSize; x++) {
                    double offsetX = x + 0.5 - origin.X;
                    double offsetY = y + 0.5 - origin.Y;
                    double localX = (offsetX * downY - offsetY * downX) / determinant;
                    double localY = (offsetY * rightX - offsetX * rightY) / determinant;
                    bool nearEdge = Math.Abs(localX) < EdgeTolerance || Math.Abs(localX - SpriteWidth) < EdgeTolerance
                        || Math.Abs(localY) < EdgeTolerance || Math.Abs(localY - SpriteHeight) < EdgeTolerance
                        || Math.Abs(localX - SpriteWidth * 0.5) < EdgeTolerance;
                    if (nearEdge) {
                        continue;
                    }

                    bool inside = localX > 0 && localX < SpriteWidth && localY > 0 && localY < SpriteHeight;
                    byte4 expected = background;
                    if (inside) {
                        expected = localX < SpriteWidth * 0.5 ? red : blue;
                        insideCount++;
                    }
                    classified++;
                    if (!pixels[y, x].Equals(expected) && mismatches.Count < 8) {
                        mismatches.Add($"({x},{y}) expected {expected} got {pixels[y, x]}");
                    }
                }
            }

            Assert.True(insideCount > SpriteWidth * SpriteHeight / 3,
                $"The expected sprite quad must lie mostly inside the target; only {insideCount} pixels were inside.");
            Assert.True(mismatches.Count == 0,
                $"Rotation {degrees} degrees: pixel coverage differs from the PS2 corner geometry with origin "
                + $"({origin.X}, {origin.Y}) out of {classified} classified pixels: " + string.Join("; ", mismatches));
        }

        /// <summary>Creates an initialized runtime entity on the layer rendered by the test camera.</summary>
        /// <param name="core">Core that owns the entity.</param>
        /// <returns>Initialized entity on layer 1.</returns>
        static Entity CreateEntity(Core core) {
            Entity entity = new Entity(core) { LayerMask = 1 };
            entity.InitComponents();
            entity.InitChildren();
            return entity;
        }

        /// <summary>Copies a synthetic render target to the CPU without reading any desktop or window pixels.</summary>
        /// <param name="target">Offscreen target produced by the test.</param>
        /// <param name="device">Device owning the target.</param>
        /// <returns>RGBA pixels indexed by row, then column.</returns>
        static byte4[,] ReadPixels(RenderTarget target, Device device) {
            DirectX11RenderTargetResource resource = Assert.IsType<DirectX11RenderTargetResource>(target);
            Texture2DDescription description = resource.ColorTexture.Description;
            description.BindFlags = BindFlags.None;
            description.Usage = ResourceUsage.Staging;
            description.CpuAccessFlags = CpuAccessFlags.Read;
            description.OptionFlags = ResourceOptionFlags.None;
            using Texture2D staging = new Texture2D(device, description);
            device.ImmediateContext.CopyResource(resource.ColorTexture, staging);
            SharpDX.DataBox mapped = device.ImmediateContext.MapSubresource(staging, 0, MapMode.Read, MapFlags.None);
            try {
                byte4[,] pixels = new byte4[description.Height, description.Width];
                for (int row = 0; row < description.Height; row++) {
                    for (int column = 0; column < description.Width; column++) {
                        int offset = row * mapped.RowPitch + column * 4;
                        pixels[row, column] = new byte4(Marshal.ReadByte(mapped.DataPointer, offset),
                            Marshal.ReadByte(mapped.DataPointer, offset + 1),
                            Marshal.ReadByte(mapped.DataPointer, offset + 2),
                            Marshal.ReadByte(mapped.DataPointer, offset + 3));
                    }
                }
                return pixels;
            } finally {
                device.ImmediateContext.UnmapSubresource(staging, 0);
            }
        }
    }
}
