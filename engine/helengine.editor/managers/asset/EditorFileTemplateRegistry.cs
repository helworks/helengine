namespace helengine.editor {
    /// <summary>
    /// Registers and exposes file templates available to the editor.
    /// </summary>
    public static class EditorFileTemplateRegistry {
        /// <summary>
        /// Extension used for shader source files.
        /// </summary>
        public const string ShaderExtension = ".hlsl";
        /// <summary>
        /// Extension used for serialized material assets.
        /// </summary>
        public const string MaterialExtension = ".hasset";

        /// <summary>
        /// Default shader source contents used for new shader files.
        /// </summary>
        const string DefaultShaderContents =
            "struct VS_IN\n" +
            "{\n" +
            "    float3 pos : POSITION;\n" +
            "    float3 normal : NORMAL;\n" +
            "    float2 texCoord : TEXCOORD0;\n" +
            "};\n" +
            "\n" +
            "struct PS_IN\n" +
            "{\n" +
            "    float4 pos : SV_POSITION;\n" +
            "};\n" +
            "\n" +
            "cbuffer TransformBuffer : register(b0)\n" +
            "{\n" +
            "    float4x4 worldViewProj;\n" +
            "};\n" +
            "\n" +
            "PS_IN VS(VS_IN input)\n" +
            "{\n" +
            "    PS_IN output;\n" +
            "    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);\n" +
            "    return output;\n" +
            "}\n" +
            "\n" +
            "float4 PS(PS_IN input) : SV_Target\n" +
            "{\n" +
            "    return float4(0.2, 0.6, 0.9, 1.0);\n" +
            "}\n";

        /// <summary>
        /// Backing list of registered templates.
        /// </summary>
        static readonly List<EditorFileTemplate> Templates;

        /// <summary>
        /// Initializes the template registry with default templates.
        /// </summary>
        static EditorFileTemplateRegistry() {
            Templates = new List<EditorFileTemplate>(5);
            RegisterDefaults();
        }

        /// <summary>
        /// Gets the registered templates in display order.
        /// </summary>
        public static IReadOnlyList<EditorFileTemplate> RegisteredTemplates => Templates;

        /// <summary>
        /// Registers a new file template for editor use.
        /// </summary>
        /// <param name="template">Template to register.</param>
        public static void Register(EditorFileTemplate template) {
            if (template == null) {
                throw new ArgumentNullException(nameof(template));
            }

            Templates.Add(template);
        }

        /// <summary>
        /// Tries to find the first template matching the specified kind.
        /// </summary>
        /// <param name="kind">Template kind to locate.</param>
        /// <param name="template">Matching template when found.</param>
        /// <returns>True when a matching template was found.</returns>
        public static bool TryGetTemplate(EditorFileTemplateKind kind, out EditorFileTemplate template) {
            for (int i = 0; i < Templates.Count; i++) {
                EditorFileTemplate candidate = Templates[i];
                if (candidate != null && candidate.Kind == kind) {
                    template = candidate;
                    return true;
                }
            }

            template = null;
            return false;
        }

        /// <summary>
        /// Registers the built-in shader and material templates.
        /// </summary>
        static void RegisterDefaults() {
            Register(BuildBlueprintTemplate());
            Register(BuildMaterialTemplate());
            Register(BuildShaderTemplate());
        }

        /// <summary>
        /// Builds the default blueprint template.
        /// </summary>
        /// <returns>Blueprint template definition.</returns>
        static EditorFileTemplate BuildBlueprintTemplate() {
            return new EditorFileTemplate(
                "Blueprint",
                "New Blueprint",
                BlueprintAsset.FileExtension,
                EditorFileTemplateKind.Blueprint,
                string.Empty);
        }

        /// <summary>
        /// Builds the default material template.
        /// </summary>
        /// <returns>Material template definition.</returns>
        static EditorFileTemplate BuildMaterialTemplate() {
            return new EditorFileTemplate(
                "Material",
                "New Material",
                MaterialExtension,
                EditorFileTemplateKind.Material,
                string.Empty);
        }

        /// <summary>
        /// Builds the default shader template.
        /// </summary>
        /// <returns>Shader template definition.</returns>
        static EditorFileTemplate BuildShaderTemplate() {
            return new EditorFileTemplate(
                "Shader",
                "New Shader",
                ShaderExtension,
                EditorFileTemplateKind.Shader,
                DefaultShaderContents);
        }
    }
}
