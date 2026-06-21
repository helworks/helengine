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

            RuntimeSubmesh[] submeshes = ResolveSubmeshes(drawable.Model);
            RenderFrameDrawableSubmission[] submissions = new RenderFrameDrawableSubmission[submeshes.Length];
            for (int submeshIndex = 0; submeshIndex < submeshes.Length; submeshIndex++) {
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
        /// Resolves the runtime submeshes that should become render submissions for the supplied model.
        /// </summary>
        /// <param name="model">Runtime model referenced by the drawable.</param>
        /// <returns>Runtime submeshes that should become render submissions.</returns>
        static RuntimeSubmesh[] ResolveSubmeshes(RuntimeModel model) {
            if (model == null || model.Submeshes == null || model.Submeshes.Length == 0) {
                return new[] {
                    new RuntimeSubmesh {
                        MaterialSlotName = string.Empty,
                        IndexStart = 0,
                        IndexCount = 0
                    }
                };
            }

            return model.Submeshes;
        }

        /// <summary>
        /// Resolves the runtime material bound to one submesh slot.
        /// </summary>
        /// <param name="drawable">Drawable that owns the material slots.</param>
        /// <param name="submeshIndex">Zero-based submesh index to resolve.</param>
        /// <returns>Runtime material bound to the requested submesh slot.</returns>
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
