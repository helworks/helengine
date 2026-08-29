namespace helengine.editor {
    /// <summary>
    /// Builds line-based bounds overlays for the offscreen model preview scene.
    /// </summary>
    public static class ModelPreviewBoundsOverlayFactory {
        /// <summary>
        /// Number of segments used by each great circle in the bounding-sphere overlay.
        /// </summary>
        const int SphereSegments = 32;
        /// <summary>
        /// Render order that keeps bounds lines above the model and floor grid.
        /// </summary>
        const byte BoundsRenderOrder3D = 2;

        /// <summary>
        /// Creates one line-rendered bounding-box entity centered at the local origin.
        /// </summary>
        /// <param name="render3D">Renderer used to build the overlay runtime resources.</param>
        /// <param name="halfExtents">Half-size of the model bounds on each axis.</param>
        /// <returns>Configured bounding-box overlay entity.</returns>
        public static EditorEntity CreateBox(RenderManager3D render3D, float3 halfExtents, EngineGeneratedMaterialCache generatedMaterialCache) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }
            if (halfExtents.X < 0f || halfExtents.Y < 0f || halfExtents.Z < 0f) {
                throw new ArgumentOutOfRangeException(nameof(halfExtents), "Bounding-box half extents cannot be negative.");
            }
            if (generatedMaterialCache == null) {
                throw new ArgumentNullException(nameof(generatedMaterialCache));
            }

            ModelAsset modelAsset = CreateBoxModelAsset(halfExtents);
            RuntimeModel model = CreateLineRuntimeModel(render3D, modelAsset);
            Core ownerCore = render3D.OwnerCore ?? throw new InvalidOperationException("Preview bounds renderer must be bound to an owning core.");
            return CreateOverlayEntity("Model Preview Bounds Box", model, generatedMaterialCache, ownerCore);
        }

        /// <summary>
        /// Creates one line-rendered bounding-sphere entity centered at the local origin.
        /// </summary>
        /// <param name="render3D">Renderer used to build the overlay runtime resources.</param>
        /// <param name="radius">Radius of the sphere that encloses the model bounds.</param>
        /// <returns>Configured bounding-sphere overlay entity.</returns>
        public static EditorEntity CreateSphere(RenderManager3D render3D, float radius, EngineGeneratedMaterialCache generatedMaterialCache) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }
            if (radius <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(radius), "Bounding-sphere radius must be greater than zero.");
            }
            if (generatedMaterialCache == null) {
                throw new ArgumentNullException(nameof(generatedMaterialCache));
            }

            ModelAsset modelAsset = CreateSphereModelAsset(radius);
            RuntimeModel model = CreateLineRuntimeModel(render3D, modelAsset);
            Core ownerCore = render3D.OwnerCore ?? throw new InvalidOperationException("Preview bounds renderer must be bound to an owning core.");
            return CreateOverlayEntity("Model Preview Bounds Sphere", model, generatedMaterialCache, ownerCore);
        }

        /// <summary>
        /// Creates an internal model-preview entity with an overlay line mesh.
        /// </summary>
        /// <param name="name">Diagnostic name assigned to the overlay entity.</param>
        /// <param name="model">Line-list model drawn by the entity.</param>
        /// <returns>Configured overlay entity.</returns>
        static EditorEntity CreateOverlayEntity(string name, RuntimeModel model, EngineGeneratedMaterialCache generatedMaterialCache, Core ownerCore) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Overlay entity name must be provided.", nameof(name));
            }
            if (model == null) {
                throw new ArgumentNullException(nameof(model));
            }
            if (ownerCore == null) {
                throw new ArgumentNullException(nameof(ownerCore));
            }

            RuntimeMaterial material = EditorVisualMaterialFactory.CreateOverlayStandardMaterial(generatedMaterialCache);
            var entity = new EditorEntity(ownerCore) {
                Name = name,
                Hidden = true,
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneModelPreview,
                Enabled = false
            };
            var mesh = new MeshComponent {
                Model = model,
                Materials = new[] { material },
                RenderOrder3D = BoundsRenderOrder3D
            };
            entity.AddComponent(mesh);
            return entity;
        }

        /// <summary>
        /// Builds a wireframe box model centered at the local origin.
        /// </summary>
        /// <param name="halfExtents">Half-size of the box on each axis.</param>
        /// <returns>Raw line-list model asset.</returns>
        static ModelAsset CreateBoxModelAsset(float3 halfExtents) {
            float x = halfExtents.X;
            float y = halfExtents.Y;
            float z = halfExtents.Z;
            float3[] positions = new[] {
                new float3(-x, -y, -z), new float3(x, -y, -z), new float3(x, y, -z), new float3(-x, y, -z),
                new float3(-x, -y, z), new float3(x, -y, z), new float3(x, y, z), new float3(-x, y, z)
            };
            ushort[] indices = new ushort[] {
                0, 1, 1, 2, 2, 3, 3, 0,
                4, 5, 5, 6, 6, 7, 7, 4,
                0, 4, 1, 5, 2, 6, 3, 7
            };
            return CreateLineModelAsset(positions, indices, new float3(-x, -y, -z), new float3(x, y, z));
        }

        /// <summary>
        /// Builds a wireframe sphere from three orthogonal great circles.
        /// </summary>
        /// <param name="radius">Radius assigned to every great circle.</param>
        /// <returns>Raw line-list model asset.</returns>
        static ModelAsset CreateSphereModelAsset(float radius) {
            List<float3> positions = new List<float3>(SphereSegments * 3);
            List<ushort> indices = new List<ushort>(SphereSegments * 6);
            AddGreatCircle(positions, indices, radius, 0);
            AddGreatCircle(positions, indices, radius, 1);
            AddGreatCircle(positions, indices, radius, 2);
            return CreateLineModelAsset(
                positions.ToArray(),
                indices.ToArray(),
                new float3(-radius, -radius, -radius),
                new float3(radius, radius, radius));
        }

        /// <summary>
        /// Appends one great-circle line loop in the requested principal plane.
        /// </summary>
        /// <param name="positions">Destination vertex positions.</param>
        /// <param name="indices">Destination line-list index stream.</param>
        /// <param name="radius">Circle radius.</param>
        /// <param name="planeIndex">Zero for XY, one for XZ, or two for YZ.</param>
        static void AddGreatCircle(List<float3> positions, List<ushort> indices, float radius, int planeIndex) {
            if (positions == null) {
                throw new ArgumentNullException(nameof(positions));
            }
            if (indices == null) {
                throw new ArgumentNullException(nameof(indices));
            }
            if (planeIndex < 0 || planeIndex > 2) {
                throw new ArgumentOutOfRangeException(nameof(planeIndex), "Great-circle plane index must identify XY, XZ, or YZ.");
            }

            int startIndex = positions.Count;
            for (int segmentIndex = 0; segmentIndex < SphereSegments; segmentIndex++) {
                double angle = segmentIndex / (double)SphereSegments * Math.PI * 2d;
                float cosine = (float)(Math.Cos(angle) * radius);
                float sine = (float)(Math.Sin(angle) * radius);
                if (planeIndex == 0) {
                    positions.Add(new float3(cosine, sine, 0f));
                } else if (planeIndex == 1) {
                    positions.Add(new float3(cosine, 0f, sine));
                } else {
                    positions.Add(new float3(0f, cosine, sine));
                }
            }

            for (int segmentIndex = 0; segmentIndex < SphereSegments; segmentIndex++) {
                int nextIndex = (segmentIndex + 1) % SphereSegments;
                indices.Add((ushort)(startIndex + segmentIndex));
                indices.Add((ushort)(startIndex + nextIndex));
            }
        }

        /// <summary>
        /// Builds one raw model asset with a single line-list range.
        /// </summary>
        /// <param name="positions">Line endpoint positions.</param>
        /// <param name="indices">Line-list index pairs.</param>
        /// <param name="boundsMin">Minimum model bounds.</param>
        /// <param name="boundsMax">Maximum model bounds.</param>
        /// <returns>Raw model asset ready for runtime conversion.</returns>
        static ModelAsset CreateLineModelAsset(float3[] positions, ushort[] indices, float3 boundsMin, float3 boundsMax) {
            if (positions == null) {
                throw new ArgumentNullException(nameof(positions));
            }
            if (indices == null) {
                throw new ArgumentNullException(nameof(indices));
            }

            return new ModelAsset {
                Positions = positions,
                Normals = new float3[positions.Length],
                TexCoords = new float2[positions.Length],
                Indices16 = indices,
                Submeshes = new[] {
                    new ModelSubmeshAsset {
                        MaterialSlotName = "Bounds",
                        IndexStart = 0,
                        IndexCount = indices.Length
                    }
                },
                BoundsMin = boundsMin,
                BoundsMax = boundsMax
            };
        }

        /// <summary>
        /// Builds one runtime model and marks its single draw range as a line list.
        /// </summary>
        /// <param name="render3D">Renderer used to build the model resource.</param>
        /// <param name="modelAsset">Raw line model asset.</param>
        /// <returns>Runtime model configured for line-list drawing.</returns>
        static RuntimeModel CreateLineRuntimeModel(RenderManager3D render3D, ModelAsset modelAsset) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }
            if (modelAsset == null) {
                throw new ArgumentNullException(nameof(modelAsset));
            }

            RuntimeModel model = render3D.BuildModelFromRaw(modelAsset);
            model.SetSubmeshes(new[] {
                new RuntimeSubmesh {
                    MaterialSlotName = "Bounds",
                    IndexStart = 0,
                    IndexCount = modelAsset.Indices16.Length,
                    PrimitiveTopology = ModelPrimitiveTopology.LineList
                }
            });
            return model;
        }
    }
}
