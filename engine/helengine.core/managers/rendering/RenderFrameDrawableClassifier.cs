namespace helengine {
    /// <summary>
    /// Classifies visible drawables into the shared render-frame representation.
    /// </summary>
    public sealed class RenderFrameDrawableClassifier {
        /// <summary>
        /// Creates one shared drawable submission per visible submesh from a runtime drawable.
        /// </summary>
        /// <param name="drawable">Visible drawable to classify.</param>
        /// <returns>Shared render-frame drawable submissions.</returns>
        public RenderFrameDrawableSubmission[] Classify(IDrawable3D drawable) {
            if (drawable == null) {
                throw new ArgumentNullException(nameof(drawable));
            }

            int submeshCount = drawable.Model == null || drawable.Model.Submeshes == null || drawable.Model.Submeshes.Length == 0
                ? 1
                : drawable.Model.Submeshes.Length;
            RenderFrameDrawableSubmission[] submissions = new RenderFrameDrawableSubmission[submeshCount];
            for (int submeshIndex = 0; submeshIndex < submeshCount; submeshIndex++) {
                RuntimeMaterial material = ResolveMaterial(drawable, submeshIndex);
                submissions[submeshIndex] = new RenderFrameDrawableSubmission(
                    drawable,
                    submeshIndex,
                    material,
                    IsTransparent(material),
                    new RenderFrameBatchingMetadata(false, false, false));
            }

            return submissions;
        }

        /// <summary>
        /// Resolves the runtime material bound to one submesh slot.
        /// </summary>
        /// <param name="drawable">Drawable that owns the material slots.</param>
        /// <param name="submeshIndex">Zero-based submesh index to resolve.</param>
        /// <returns>A drawable-owned runtime material borrowed for frame classification.</returns>
        [NativeBorrowedReturn]
        static RuntimeMaterial ResolveMaterial(IDrawable3D drawable, int submeshIndex) {
            if (drawable == null) {
                throw new ArgumentNullException(nameof(drawable));
            } else if (submeshIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(submeshIndex), "Submesh index must be non-negative.");
            }

            RuntimeMaterial[] materials = drawable.Materials;
            if (materials == null || materials.Length == 0) {
                return null;
            }
            if (submeshIndex < materials.Length) {
                return materials[submeshIndex];
            }

            return materials[0];
        }

        /// <summary>
        /// Returns whether the supplied runtime material should be rendered in the transparent pass.
        /// </summary>
        /// <param name="material">Runtime material to inspect.</param>
        /// <returns>True when the material uses alpha blending.</returns>
        static bool IsTransparent(RuntimeMaterial material) {
            if (material == null) {
                return false;
            }

            MaterialRenderState renderState = material.RenderState;
            return renderState != null && renderState.BlendMode == MaterialBlendMode.AlphaBlend;
        }
    }
}
