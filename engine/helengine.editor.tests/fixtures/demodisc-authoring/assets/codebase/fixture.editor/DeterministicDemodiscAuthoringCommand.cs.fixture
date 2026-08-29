using System;

namespace fixture.editor {
    /// <summary>
    /// Minimal generated-project command used by the deterministic authoring fixture.
    /// </summary>
    public sealed class DeterministicDemodiscAuthoringCommand : IEditorCommand {
        /// <summary>Stable command identifier discovered by the editor host.</summary>
        public string CommandId => "fixture.generate-deterministic-assets";

        /// <summary>Label surfaced by the generated editor command catalog.</summary>
        public string DisplayName => "Generate deterministic fixture assets";

        /// <summary>
        /// Authors all fixture outputs through one public-session transaction.
        /// </summary>
        /// <param name="context">Public command context supplied by the editor host.</param>
        public void Execute(IEditorCommandContext context) {
            using (EditorAuthoringTransaction transaction = context.Authoring.BeginTransaction()) {
                EditorAssetWriteResult model = transaction.WriteAsset("models/generated.hasset", CreateModel("Generated", 1f));
                EditorAssetWriteResult copy = transaction.WriteAsset("models/generated-copy.hasset", CreateModel("GeneratedCopy", 2f));
                EditorAssetWriteResult material = transaction.WriteMaterial("materials/generated.hasset", CreateMaterial("GeneratedMaterial"));
                Console.WriteLine("FIXTURE_WRITE|" + Format(model));
                Console.WriteLine("FIXTURE_WRITE|" + Format(copy));
                Console.WriteLine("FIXTURE_WRITE|" + Format(material));
                transaction.Commit();
            }
        }

        static string Format(EditorAssetWriteResult result) {
            return result.RelativePath + "|" + result.AssetId + "|" + result.ContentHash + "|" + result.Disposition;
        }

        static ModelAsset CreateModel(string id, float positionOffset) {
            return new ModelAsset {
                Id = id,
                Positions = new[] {
                    new float3(positionOffset, 0f, 0f),
                    new float3(0f, 1f, 0f),
                    new float3(0f, 0f, 1f)
                },
                Normals = new[] {
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f)
                },
                TexCoords = new[] {
                    new float2(0f, 0f),
                    new float2(1f, 0f),
                    new float2(0f, 1f)
                },
                Indices16 = new ushort[] { 0, 1, 2 },
                Indices32 = Array.Empty<uint>(),
                Submeshes = Array.Empty<ModelSubmeshAsset>()
            };
        }

        static GeneratedMaterialAssetDefinition CreateMaterial(string id) {
            GeneratedMaterialAssetDefinition definition = new GeneratedMaterialAssetDefinition {
                MaterialAsset = new MaterialAsset {
                    Id = id,
                    RenderState = new MaterialRenderState(),
                    CastsShadows = true,
                    ReceivesShadows = true
                }
            };
            GeneratedMaterialPlatformDefinition windows = definition.GetOrCreatePlatform("windows");
            windows.SchemaId = "standard-shader";
            windows.SetFieldValue("use-custom-shader", "false");
            windows.SetFieldValue("casts-shadow", "true");
            windows.SetFieldValue("receives-shadow", "true");
            windows.SetFieldValue("base-color", "#FFFFFFFF");
            GeneratedMaterialPlatformDefinition ps2 = definition.GetOrCreatePlatform("ps2");
            ps2.SchemaId = "ps2-simple-lit";
            ps2.SetFieldValue("double-sided", "true");
            return definition;
        }
    }
}
