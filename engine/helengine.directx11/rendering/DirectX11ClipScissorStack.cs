using SharpDX.Direct3D11;
using D3DDevice = SharpDX.Direct3D11.Device;

namespace helengine.directx11 {
    /// <summary>
    /// Applies the shared 2D clip stack to the DirectX11 rasterizer scissor rectangle.
    /// </summary>
    public class DirectX11ClipScissorStack : ClipScissorStack2D {
        /// <summary>
        /// Device whose immediate context receives the scissor rectangles.
        /// </summary>
        readonly D3DDevice Device;

        /// <summary>
        /// Left edge of the current camera scissor rectangle.
        /// </summary>
        int CameraScissorLeft;

        /// <summary>
        /// Top edge of the current camera scissor rectangle.
        /// </summary>
        int CameraScissorTop;

        /// <summary>
        /// Right edge of the current camera scissor rectangle.
        /// </summary>
        int CameraScissorRight;

        /// <summary>
        /// Bottom edge of the current camera scissor rectangle.
        /// </summary>
        int CameraScissorBottom;

        /// <summary>Physical camera viewport that receives the projected logical clip rectangles.</summary>
        float4 CameraViewport;

        /// <summary>Logical canvas projected into the physical camera viewport.</summary>
        float4 ProjectionViewport;

        /// <summary>
        /// Initializes the stack against the device that receives its scissor rectangles.
        /// </summary>
        /// <param name="device">Device whose immediate context receives the scissor rectangles.</param>
        public DirectX11ClipScissorStack(D3DDevice device) {
            if (device == null) {
                throw new ArgumentNullException(nameof(device));
            }

            Device = device;
        }

        /// <summary>
        /// Records the camera viewport that unclipped drawables scissor against, and applies it immediately.
        /// </summary>
        /// <param name="viewport">Camera viewport in pixel-space coordinates.</param>
        public void SetCameraViewport(float4 viewport) {
            CameraViewport = viewport;
            ProjectionViewport = viewport;
            CameraScissorLeft = (int)RealMath.Round(viewport.X);
            CameraScissorTop = (int)RealMath.Round(viewport.Y);
            CameraScissorRight = (int)RealMath.Round(viewport.X + viewport.Z);
            CameraScissorBottom = (int)RealMath.Round(viewport.Y + viewport.W);
            ApplyCameraScissor();
        }

        /// <summary>Sets the logical projection used to translate drawable clipping into physical target pixels.</summary>
        /// <param name="viewport">Finite logical viewport with positive dimensions.</param>
        public void SetProjectionViewport(float4 viewport) {
            if (!float.IsFinite((float)(viewport.X)) || !float.IsFinite((float)(viewport.Y)) ||
                !float.IsFinite((float)(viewport.Z)) || !float.IsFinite((float)(viewport.W)) || viewport.Z <= 0f || viewport.W <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(viewport), "Projection viewport must be finite and have positive dimensions.");
            }
            ProjectionViewport = viewport;
        }

        /// <summary>
        /// Restores the camera viewport scissor after the clip stack empties.
        /// </summary>
        protected override void ApplyCameraScissor() {
            Device.ImmediateContext.Rasterizer.SetScissorRectangle(
                CameraScissorLeft,
                CameraScissorTop,
                CameraScissorRight,
                CameraScissorBottom);
        }

        /// <summary>
        /// Applies one resolved clip rectangle to the active DirectX scissor state.
        /// </summary>
        /// <param name="clipRect">Logical clip rectangle resolved for the current drawable.</param>
        protected override void ApplyClipScissor(float4 clipRect) {
            double scaleX = (double)((double)CameraViewport.Z / ProjectionViewport.Z);
            double scaleY = (double)((double)CameraViewport.W / ProjectionViewport.W);
            float4 physicalClipRect = new float4(
                (float)(CameraViewport.X + (((double)clipRect.X - ProjectionViewport.X) * scaleX)),
                (float)(CameraViewport.Y + (((double)clipRect.Y - ProjectionViewport.Y) * scaleY)),
                (float)(clipRect.Z * scaleX),
                (float)(clipRect.W * scaleY));
            float4 viewportRect = new float4(CameraScissorLeft, CameraScissorTop, CameraScissorRight - CameraScissorLeft, CameraScissorBottom - CameraScissorTop);
            float4 effectiveRect = Intersect(viewportRect, physicalClipRect);

            int scissorLeft = (int)RealMath.Round(effectiveRect.X);
            int scissorTop = (int)RealMath.Round(effectiveRect.Y);
            int scissorRight = (int)RealMath.Round(effectiveRect.X + effectiveRect.Z);
            int scissorBottom = (int)RealMath.Round(effectiveRect.Y + effectiveRect.W);
            Device.ImmediateContext.Rasterizer.SetScissorRectangle(scissorLeft, scissorTop, scissorRight, scissorBottom);
        }
    }
}
