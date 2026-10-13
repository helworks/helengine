namespace helengine.baseplatform.Player {

    /// <summary>
    /// Platform-neutral description of one cooked material, produced by a platform player adapter so the desktop
    /// player can render platform-cooked content without understanding the platform's material format.
    /// </summary>
    public sealed class PlayerMaterialDescription {
        /// <summary>
        /// Initializes one decoded material description.
        /// </summary>
        /// <param name="baseColorR">Red channel of the material base color, 0-255.</param>
        /// <param name="baseColorG">Green channel of the material base color, 0-255.</param>
        /// <param name="baseColorB">Blue channel of the material base color, 0-255.</param>
        /// <param name="baseColorA">Alpha channel of the material base color, 0-255.</param>
        /// <param name="diffuseTextureRelativePath">Content-root-relative path of the diffuse texture, or <see cref="string.Empty"/> when the material has no texture.</param>
        /// <param name="renderClass">How the player must blend the material.</param>
        /// <param name="doubleSided">Whether back faces must be drawn instead of culled.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="diffuseTextureRelativePath"/> is null; untextured materials pass <see cref="string.Empty"/>.</exception>
        public PlayerMaterialDescription(
            byte baseColorR,
            byte baseColorG,
            byte baseColorB,
            byte baseColorA,
            string diffuseTextureRelativePath,
            PlayerMaterialRenderClass renderClass,
            bool doubleSided) {
            if (diffuseTextureRelativePath == null) {
                throw new ArgumentNullException(nameof(diffuseTextureRelativePath), "Diffuse texture path is required; pass an empty string for untextured materials.");
            }

            BaseColorR = baseColorR;
            BaseColorG = baseColorG;
            BaseColorB = baseColorB;
            BaseColorA = baseColorA;
            DiffuseTextureRelativePath = diffuseTextureRelativePath;
            RenderClass = renderClass;
            DoubleSided = doubleSided;
        }

        /// <summary>
        /// Gets the red channel of the material base color, 0-255.
        /// </summary>
        public byte BaseColorR { get; }

        /// <summary>
        /// Gets the green channel of the material base color, 0-255.
        /// </summary>
        public byte BaseColorG { get; }

        /// <summary>
        /// Gets the blue channel of the material base color, 0-255.
        /// </summary>
        public byte BaseColorB { get; }

        /// <summary>
        /// Gets the alpha channel of the material base color, 0-255.
        /// </summary>
        public byte BaseColorA { get; }

        /// <summary>
        /// Gets the content-root-relative path of the diffuse texture, or <see cref="string.Empty"/> when the material is untextured. Never null.
        /// </summary>
        public string DiffuseTextureRelativePath { get; }

        /// <summary>
        /// Gets how the player must blend the material.
        /// </summary>
        public PlayerMaterialRenderClass RenderClass { get; }

        /// <summary>
        /// Gets whether back faces must be drawn instead of culled.
        /// </summary>
        public bool DoubleSided { get; }
    }
}
