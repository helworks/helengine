using System.Runtime.InteropServices;
using helengine.directx11;
using SharpDX.Direct3D11;

namespace helengine.player.tests {
    /// <summary>
    /// DirectX11 renderer that copies each swap-chain back buffer to the CPU right before it is presented, so tests can
    /// assert the exact pixels a window would receive without reading anything from the desktop.
    /// </summary>
    public sealed class CapturingDirectX11Renderer3D : DirectX11Renderer3D {
        /// <summary>
        /// Gets the BGRA pixels of the most recently presented back buffer, indexed by row then column, or null before
        /// the first present.
        /// </summary>
        public byte4[,] LastPresentedPixels { get; private set; }

        /// <summary>
        /// Gets the size the presented surface reported to its camera passes (<see cref="DirectX11SwapChainSurface.Width"/>
        /// and <see cref="DirectX11SwapChainSurface.Height"/>) at the most recent present.
        /// </summary>
        public int2 LastPresentedSurfaceSize { get; private set; }

        /// <summary>
        /// Gets the size of the depth buffer the camera passes used on the presented surface at the most recent present.
        /// </summary>
        public int2 LastPresentedDepthBufferSize { get; private set; }

        /// <summary>
        /// Captures the back buffer that is about to be presented, then presents it through the normal path.
        /// </summary>
        /// <param name="surface">Surface whose back buffer is being presented.</param>
        protected override void PresentSurface(DirectX11SwapChainSurface surface) {
            LastPresentedSurfaceSize = new int2(surface.Width, surface.Height);
            Texture2DDescription depthDescription = surface.DepthBuffer.Description;
            LastPresentedDepthBufferSize = new int2(depthDescription.Width, depthDescription.Height);
            using (Texture2D backBuffer = surface.SwapChain.GetBackBuffer<Texture2D>(0)) {
                LastPresentedPixels = ReadPixels(backBuffer);
            }

            base.PresentSurface(surface);
        }

        /// <summary>
        /// Copies a GPU texture into a staging copy and reads its pixels.
        /// </summary>
        /// <param name="texture">Texture to read; a 4-bytes-per-pixel format.</param>
        /// <returns>Pixels in the texture's own byte order, indexed by row then column.</returns>
        byte4[,] ReadPixels(Texture2D texture) {
            Texture2DDescription description = texture.Description;
            description.BindFlags = BindFlags.None;
            description.Usage = ResourceUsage.Staging;
            description.CpuAccessFlags = CpuAccessFlags.Read;
            description.OptionFlags = ResourceOptionFlags.None;
            using Texture2D staging = new Texture2D(Device, description);
            Device.ImmediateContext.CopyResource(texture, staging);
            SharpDX.DataBox mapped = Device.ImmediateContext.MapSubresource(staging, 0, MapMode.Read, MapFlags.None);
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
                Device.ImmediateContext.UnmapSubresource(staging, 0);
            }
        }
    }
}
