namespace helengine.editor {
    /// <summary>
    /// Builds shared line-list wireframe entities used by component scene selection editors.
    /// </summary>
    public static class ComponentSelectionWireframeFactory {
        /// <summary>
        /// Creates one internal unit line-box entity whose scale callers set to the visualized world-space size.
        /// </summary>
        /// <param name="render3D">Renderer used to build the wireframe runtime resources.</param>
        /// <param name="name">Diagnostic name assigned to the wireframe entity.</param>
        /// <param name="renderOrder3D">Render order applied to the wireframe mesh.</param>
        /// <returns>Owned internal wireframe entity.</returns>
        public static EditorEntity CreateUnitLineBox(RenderManager3D render3D, EngineGeneratedMaterialCache generatedMaterialCache, string name, byte renderOrder3D) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            } else if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Wireframe entity name must be provided.", nameof(name));
            }
            if (generatedMaterialCache == null) {
                throw new ArgumentNullException(nameof(generatedMaterialCache));
            }

            RuntimeModel model = CreateLineRuntimeModel(render3D, CreateUnitBoxModelAsset());
            RuntimeMaterial material = EditorVisualMaterialFactory.CreateOverlayStandardMaterial(generatedMaterialCache);
            EditorEntity entity = new EditorEntity {
                Name = name,
                Hidden = true,
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneObjects
            };
            entity.AddComponent(new MeshComponent {
                Model = model,
                Materials = new[] { material },
                RenderOrder3D = renderOrder3D
            });
            return entity;
        }

        /// <summary>
        /// Builds one unit wireframe box model centered at the local origin.
        /// </summary>
        /// <returns>Raw line-list model asset spanning one unit on every axis.</returns>
        static ModelAsset CreateUnitBoxModelAsset() {
            const float half = 0.5f;
            float3[] positions = new[] {
                new float3(-half, -half, -half), new float3(half, -half, -half), new float3(half, half, -half), new float3(-half, half, -half),
                new float3(-half, -half, half), new float3(half, -half, half), new float3(half, half, half), new float3(-half, half, half)
            };
            ushort[] indices = new ushort[] {
                0, 1, 1, 2, 2, 3, 3, 0,
                4, 5, 5, 6, 6, 7, 7, 4,
                0, 4, 1, 5, 2, 6, 3, 7
            };
            return new ModelAsset {
                Positions = positions,
                Normals = new float3[positions.Length],
                TexCoords = new float2[positions.Length],
                Indices16 = indices,
                Submeshes = new[] {
                    new ModelSubmeshAsset {
                        MaterialSlotName = "Wireframe",
                        IndexStart = 0,
                        IndexCount = indices.Length
                    }
                },
                BoundsMin = new float3(-half, -half, -half),
                BoundsMax = new float3(half, half, half)
            };
        }

        /// <summary>
        /// Builds one runtime model and marks its single draw range as a line list.
        /// </summary>
        /// <param name="render3D">Renderer used to build the model resource.</param>
        /// <param name="modelAsset">Raw line model asset.</param>
        /// <returns>Runtime model configured for line-list drawing.</returns>
        static RuntimeModel CreateLineRuntimeModel(RenderManager3D render3D, ModelAsset modelAsset) {
            RuntimeModel model = render3D.BuildModelFromRaw(modelAsset);
            model.SetSubmeshes(new[] {
                new RuntimeSubmesh {
                    MaterialSlotName = "Wireframe",
                    IndexStart = 0,
                    IndexCount = modelAsset.Indices16.Length,
                    PrimitiveTopology = ModelPrimitiveTopology.LineList
                }
            });
            return model;
        }
    }
}
