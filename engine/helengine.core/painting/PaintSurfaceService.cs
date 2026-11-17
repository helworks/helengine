using helengine.painting;

namespace helengine {
    /// <summary>Exposes the optional renderer-backed painting factory through normal Core services.</summary>
    public sealed class PaintSurfaceService {
        /// <summary>Borrows the host factory; its device and renderer must enclose every surface and snapshot lifetime.</summary>
        readonly IPaintSurfaceFactory Factory;
        /// <summary>Creates a service around an optional host-owned renderer factory.</summary>
        public PaintSurfaceService(IPaintSurfaceFactory factory) { Factory = factory; }
        /// <summary>Gets whether the host supplied a real painting resource provider.</summary>
        public bool IsAvailable => Factory != null;
        /// <summary>Gets packed painting-texture bytes retained by queued normal renderer frames after snapshot disposal.</summary>
        public long FrameRetainedSnapshotBytes => Factory == null ? 0 : Factory.FrameRetainedSnapshotBytes;
        /// <summary>Gets packed immutable level-zero bytes counted once across cache, material and UI leases.</summary>
        public long SnapshotBaseBytes => Factory == null ? 0 : Factory.SnapshotBaseBytes;
        /// <summary>Gets packed immutable additional-mip bytes counted once across all retained leases.</summary>
        public long SnapshotMipmapBytes => Factory == null ? 0 : Factory.SnapshotMipmapBytes;
        /// <summary>Creates a caller-owned canvas with explicit top-origin normalized clipping, or reports unavailable support.</summary>
        [NativeOwnedReturn]
        public IPaintSurface Create([NativeNoEscape] PaintCanvasDescriptor canvas, [NativeNoEscape] float[] clipTriangles) {
            if (Factory == null) { throw new NotSupportedException("Painting surfaces are unavailable on this platform."); }
            if (canvas == null || !canvas.IsValid()) { throw new ArgumentException("Invalid painting canvas."); }
            if (clipTriangles == null || clipTriangles.Length > 1860 || clipTriangles.Length % 6 != 0) {
                throw new ArgumentException("Painting clips require at most 310 canvas-clipped normalized triangles.");
            }
            for (int index = 0; index < clipTriangles.Length; index++) {
                float value = clipTriangles[index];
                if (!float.IsFinite(value) || value < 0 || value > 1) { throw new ArgumentException("Invalid normalized painting clip coordinate."); }
            }
            return Factory.Create(canvas, clipTriangles);
        }
        /// <summary>Creates the cooked generic paper material; callers bind base, overlay and appearance through its documented layout.</summary>
        [NativeOwnedReturn]
        public RuntimeMaterial CreateMaterial() {
            if (Factory == null) { throw new NotSupportedException("Painting materials are unavailable on this platform."); }
            return Factory.CreateMaterial();
        }
        /// <summary>Creates a persistent cross-face stroke identity using the native host's OS UUID facility.</summary>
        public string CreateStrokeId() {
            if (Factory == null) { throw new NotSupportedException("Painting identities are unavailable on this platform."); }
            return Factory.CreateStrokeId();
        }
        /// <summary>Binds a straight-alpha base and exact-revision premultiplied overlay to the cooked two-sided paper material.</summary>
        public void BindMaterial([NativeNoEscape] RuntimeMaterial material, [NativeRetainsBorrow] RuntimeTexture baseImage, [NativeRetainsBorrow] RuntimeTexture overlay, byte4 baseColor, int printedFrontSign) {
            BindMaterial(material, baseImage, overlay, baseColor, printedFrontSign, 1, 1);
        }
        /// <summary>Binds original base UVs and canvas overlay scaling; side mode 0 always prints for 2D, +1 prints raster front and -1 raster back. Source winding must not be applied twice.</summary>
        public void BindMaterial([NativeNoEscape] RuntimeMaterial material, [NativeRetainsBorrow] RuntimeTexture baseImage, [NativeRetainsBorrow] RuntimeTexture overlay, byte4 baseColor, int printedFrontSign, float overlayUvScaleX, float overlayUvScaleY) {
            if (Factory == null) { throw new NotSupportedException("Painting materials are unavailable on this platform."); }
            if (material == null || baseImage == null || overlay == null || baseImage.IsDisposed || overlay.IsDisposed
                || baseImage.IsPremultipliedAlpha || !overlay.IsPremultipliedAlpha || printedFrontSign < -1 || printedFrontSign > 1
                || !float.IsFinite(overlayUvScaleX) || !float.IsFinite(overlayUvScaleY) || overlayUvScaleX <= 0 || overlayUvScaleY <= 0) {
                throw new ArgumentException("Painting material requires a live straight base, premultiplied overlay and explicit printed side.");
            }
            Factory.BindMaterial(material, baseImage, overlay, baseColor, printedFrontSign, overlayUvScaleX, overlayUvScaleY);
        }
    }
}
