using helengine.directx11;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Runtime.InteropServices;

namespace helengine.editor {
    /// <summary>
    /// DirectX11 implementation of the editor picking pass and CPU pixel readback.
    /// </summary>
    public sealed class DirectX11EditorPickingBackend : IEditorPickingBackend {
        const string PickerShaderPath = "shaders\\PickerShader.fx";
        /// <summary>Renderer borrowed from the Windows editor host.</summary>
        readonly DirectX11Renderer3D Renderer;
        /// <summary>Camera whose render-target reference is cleared when owned resources are released.</summary>
        readonly CameraComponent Camera;
        /// <summary>CPU-readable staging texture used for pixel readback.</summary>
        Texture2D ReadbackTexture;
        /// <summary>Width represented by the staging texture.</summary>
        int ReadbackWidth;
        /// <summary>Height represented by the staging texture.</summary>
        int ReadbackHeight;
        /// <summary>Color format represented by the staging texture.</summary>
        Format ReadbackFormat;
        /// <summary>Render target allocated and owned by this backend.</summary>
        DirectX11RenderTargetResource Target;
        /// <summary>Tracks whether native resources have been released.</summary>
        bool IsDisposed;

        /// <summary>
        /// Initializes a backend over the caller-owned DirectX11 renderer.
        /// </summary>
        /// <param name="renderer">Renderer used to execute custom picker passes.</param>
        /// <param name="camera">Camera that receives the backend-owned render target.</param>
        public DirectX11EditorPickingBackend(DirectX11Renderer3D renderer, CameraComponent camera) {
            Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            Camera = camera ?? throw new ArgumentNullException(nameof(camera));
        }

        /// <summary>
        /// Queues a picker shader pass and ensures the camera has a backend-owned target matching its viewport.
        /// </summary>
        /// <param name="camera">Camera whose queue should be rendered.</param>
        /// <param name="colors">Colors to encode for each drawable.</param>
        public void Render(CameraComponent camera, IReadOnlyDictionary<IDrawable3D, byte4> colors) {
            ThrowIfDisposed();
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }
            if (colors == null) {
                throw new ArgumentNullException(nameof(colors));
            }

            int width = Math.Max(1, (int)Math.Ceiling(Math.Max(1.0, camera.Viewport.Z)));
            int height = Math.Max(1, (int)Math.Ceiling(Math.Max(1.0, camera.Viewport.W)));
            EnsureRenderTarget(camera, width, height);
            IRenderQueue3D queue = camera.RenderQueue3D ?? throw new InvalidOperationException("Picker camera must provide a render queue.");
            Renderer.RequestShaderPass(camera, queue, PickerShaderPath, GetPickColor(colors));
        }

        /// <summary>
        /// Copies the target into a staging texture and reads one RGBA pixel.
        /// </summary>
        /// <param name="pixel">Pixel coordinate to read.</param>
        /// <param name="color">Pixel color when readback succeeds.</param>
        /// <returns>True when the target is available and the pixel was read.</returns>
        public bool TryReadPixel(int2 pixel, out byte4 color) {
            ThrowIfDisposed();
            color = default;
            if (Target == null || Target.ColorTexture == null) {
                return false;
            }
            if (pixel.X < 0 || pixel.X >= Target.Width || pixel.Y < 0 || pixel.Y >= Target.Height) {
                return false;
            }

            EnsureReadbackTexture(Target);
            DeviceContext context = Renderer.Device.ImmediateContext;
            context.CopyResource(Target.ColorTexture, ReadbackTexture);
            DataBox dataBox = context.MapSubresource(ReadbackTexture, 0, MapMode.Read, MapFlags.None);
            try {
                int offset = pixel.Y * dataBox.RowPitch + pixel.X * 4;
                byte c0 = Marshal.ReadByte(dataBox.DataPointer, offset);
                byte c1 = Marshal.ReadByte(dataBox.DataPointer, offset + 1);
                byte c2 = Marshal.ReadByte(dataBox.DataPointer, offset + 2);
                byte c3 = Marshal.ReadByte(dataBox.DataPointer, offset + 3);
                if (Target.ColorFormat == Format.R8G8B8A8_UNorm) {
                    color = new byte4(c0, c1, c2, c3);
                    return true;
                }
                if (Target.ColorFormat == Format.B8G8R8A8_UNorm) {
                    color = new byte4(c2, c1, c0, c3);
                    return true;
                }
                throw new InvalidOperationException("Pick target format is not supported for readback.");
            } finally {
                context.UnmapSubresource(ReadbackTexture, 0);
            }
        }

        /// <summary>
        /// Releases the target and staging resources owned by this backend without disposing the renderer.
        /// </summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }

            IsDisposed = true;
            DisposeReadbackTexture();
            ClearTargetReference();
            DisposeTarget();
        }

        void EnsureRenderTarget(CameraComponent camera, int width, int height) {
            if (Target != null && Target.Width == width && Target.Height == height) {
                if (!ReferenceEquals(camera.RenderTarget, Target)) {
                    camera.RenderTarget = Target;
                }
                return;
            }

            DisposeReadbackTexture();
            ClearTargetReference();
            DisposeTarget();
            Target = (DirectX11RenderTargetResource)Renderer.CreateRenderTarget(width, height);
            camera.RenderTarget = Target;
        }

        void EnsureReadbackTexture(DirectX11RenderTargetResource target) {
            if (ReadbackTexture != null && ReadbackWidth == target.Width && ReadbackHeight == target.Height && ReadbackFormat == target.ColorFormat) {
                return;
            }

            DisposeReadbackTexture();
            Texture2DDescription description = target.ColorTexture.Description;
            if (description.SampleDescription.Count != 1 || description.SampleDescription.Quality != 0) {
                throw new InvalidOperationException("Picker readback does not support multisampled render targets.");
            }

            description.Usage = ResourceUsage.Staging;
            description.BindFlags = BindFlags.None;
            description.CpuAccessFlags = CpuAccessFlags.Read;
            description.OptionFlags = ResourceOptionFlags.None;
            ReadbackTexture = new Texture2D(Renderer.Device, description);
            ReadbackWidth = target.Width;
            ReadbackHeight = target.Height;
            ReadbackFormat = target.ColorFormat;
        }

        void DisposeReadbackTexture() {
            if (ReadbackTexture == null) {
                return;
            }

            ReadbackTexture.Dispose();
            ReadbackTexture = null;
            ReadbackWidth = 0;
            ReadbackHeight = 0;
            ReadbackFormat = Format.Unknown;
        }

        void ClearTargetReference() {
            if (ReferenceEquals(Camera.RenderTarget, Target)) {
                Camera.RenderTarget = null;
            }
        }

        void DisposeTarget() {
            if (Target == null) {
                return;
            }

            Target.Dispose();
            Target = null;
        }

        Func<IDrawable3D, byte4> GetPickColor(IReadOnlyDictionary<IDrawable3D, byte4> colors) {
            return drawable => drawable != null && colors.TryGetValue(drawable, out byte4 color)
                ? color
                : new byte4(0, 0, 0, 0);
        }

        void ThrowIfDisposed() {
            if (IsDisposed) {
                throw new ObjectDisposedException(nameof(DirectX11EditorPickingBackend));
            }
        }
    }
}
