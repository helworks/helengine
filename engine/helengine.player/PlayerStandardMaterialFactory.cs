using helengine.baseplatform.Player;
using helengine.editor;

namespace helengine.player {
    /// <summary>
    /// Builds desktop runtime materials from platform-neutral material descriptions using the built-in forward standard
    /// shader, so console-cooked materials render on DirectX 11 with their base color, diffuse texture, blending and culling.
    /// Alpha-tested materials render as opaque in this first cut.
    /// </summary>
    public sealed class PlayerStandardMaterialFactory {
        /// <summary>Built-in shader file the player renders every material with.</summary>
        public const string StandardShaderFileName = "ForwardStandardShader.hlsl";

        /// <summary>
        /// Creates a factory bound to one renderer and one built-in shader library.
        /// </summary>
        /// <param name="renderer">Renderer that compiles and owns the created materials.</param>
        /// <param name="shaders">Built-in shader library that provides the forward standard shader.</param>
        public PlayerStandardMaterialFactory(RenderManager3D renderer, EditorBuiltInShaderAssetLibrary shaders) {
            Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            Shaders = shaders ?? throw new ArgumentNullException(nameof(shaders));
        }

        /// <summary>Gets the renderer that compiles and owns created materials.</summary>
        public RenderManager3D Renderer { get; }

        /// <summary>Gets the built-in shader library that provides the forward standard shader.</summary>
        public EditorBuiltInShaderAssetLibrary Shaders { get; }

        /// <summary>
        /// Creates one standard runtime material for a decoded platform material.
        /// The base color comes from the description; roughness, metallic, specular and emissive use the same defaults
        /// as the editor's preview of fixed-pipeline materials.
        /// </summary>
        /// <param name="description">Decoded platform-neutral material description.</param>
        /// <param name="diffuseTexture">Diffuse texture to bind, or null when the material has none (the white default stays bound).</param>
        /// <returns>New runtime material owned by the caller.</returns>
        public RuntimeMaterial Create(PlayerMaterialDescription description, RuntimeTexture diffuseTexture) {
            if (description == null) {
                throw new ArgumentNullException(nameof(description));
            }

            Core ownerCore = Renderer.OwnerCore ?? throw new InvalidOperationException("The player renderer must be attached to a core before materials are created.");
            RuntimeMaterial material = EditorSessionShaderMaterialBuilder.BuildBuiltInRuntimeMaterial(Renderer, Shaders, StandardShaderFileName);
            ShaderRuntimeMaterial shaderMaterial = ShaderRuntimeMaterialAccess.Require(material);
            StandardMaterialTextureBindingDefaults.Apply(shaderMaterial, ownerCore.RenderManager2D);

            float4 baseColor = new float4(
                (float)(description.BaseColorR / 255d),
                (float)(description.BaseColorG / 255d),
                (float)(description.BaseColorB / 255d),
                (float)(description.BaseColorA / 255d));
            shaderMaterial.Properties.SetConstantBufferData(StandardMaterialBaseColorDefaults.BaseColorBufferName, StandardMaterialBaseColorDefaults.CreateConstantBufferData(baseColor));
            shaderMaterial.Properties.SetConstantBufferData(StandardMaterialRoughnessDefaults.RoughnessBufferName, StandardMaterialRoughnessDefaults.CreateDefaultConstantBufferData());
            shaderMaterial.Properties.SetConstantBufferData(StandardMaterialMetallicDefaults.MetallicBufferName, StandardMaterialMetallicDefaults.CreateDefaultConstantBufferData());
            shaderMaterial.Properties.SetConstantBufferData(StandardMaterialSpecularDefaults.SpecularBufferName, StandardMaterialSpecularDefaults.CreateDefaultConstantBufferData());
            shaderMaterial.Properties.SetConstantBufferData(StandardMaterialEmissiveColorDefaults.EmissiveColorBufferName, StandardMaterialEmissiveColorDefaults.CreateDefaultConstantBufferData());

            if (diffuseTexture != null) {
                shaderMaterial.Properties.SetTexture(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName, diffuseTexture);
            }

            material.RenderState.BlendMode = description.RenderClass == PlayerMaterialRenderClass.Transparent
                ? MaterialBlendMode.AlphaBlend
                : MaterialBlendMode.Opaque;
            material.RenderState.CullMode = description.DoubleSided ? MaterialCullMode.None : MaterialCullMode.Back;
            return material;
        }
    }
}
