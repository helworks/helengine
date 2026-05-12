using helengine.editor;
using helengine.files;

namespace helengine.demo_disc_scene_writer {
    /// <summary>
    /// Builds the canonical authored scene asset for the directional-shadow plaza showcase.
    /// </summary>
    public sealed class DirectionalShadowPlazaSceneAssetFactory {
        /// <summary>
        /// Stable scene id used by the generated directional-shadow plaza asset.
        /// </summary>
        public const string SceneId = "Scenes/rendering/directional-shadow-plaza.helen";

        /// <summary>
        /// Stable serialized component identifier used by mesh records.
        /// </summary>
        const string MeshComponentTypeId = "helengine.MeshComponent";

        /// <summary>
        /// Stable serialized component identifier used by camera records.
        /// </summary>
        const string CameraComponentTypeId = "helengine.CameraComponent";

        /// <summary>
        /// Stable serialized component identifier used by directional-light records.
        /// </summary>
        const string DirectionalLightComponentTypeId = "helengine.DirectionalLightComponent";

        /// <summary>
        /// Layer mask used by user-authored scene objects in packaged runtime scenes.
        /// </summary>
        const ushort SceneObjectsLayerMask = 0b0100000000000000;

        /// <summary>
        /// Stable save-state slot name used for serialized mesh model references.
        /// </summary>
        const string MeshModelReferenceName = "Model";

        /// <summary>
        /// Stable save-state slot name used for serialized mesh material references.
        /// </summary>
        const string MeshMaterialReferenceName = "Material";

        /// <summary>
        /// Stable save-state slot name used for serialized font references.
        /// </summary>
        const string FontReferenceName = "Font";

        /// <summary>
        /// Descriptor used to serialize authored mesh payloads for committed editor scenes.
        /// </summary>
        readonly MeshComponentPersistenceDescriptor MeshDescriptor;

        /// <summary>
        /// Descriptor used to serialize authored directional-light payloads for committed editor scenes.
        /// </summary>
        readonly DirectionalLightComponentPersistenceDescriptor DirectionalLightDescriptor;

        /// <summary>
        /// Placeholder runtime model used only to satisfy authored mesh serialization before stable asset references are applied.
        /// </summary>
        readonly AuthoringPlaceholderRuntimeModel PlaceholderModel;

        /// <summary>
        /// Placeholder runtime material used only to satisfy authored mesh serialization before stable asset references are applied.
        /// </summary>
        readonly RuntimeMaterial PlaceholderMaterial;

        /// <summary>
        /// Descriptor used to serialize the FPS overlay component on the showcase camera.
        /// </summary>
        readonly FPSComponentPersistenceDescriptor FpsDescriptor;

        /// <summary>
        /// Initializes the directional-shadow plaza scene factory with the persistence descriptors required for authored output.
        /// </summary>
        public DirectionalShadowPlazaSceneAssetFactory() {
            MeshDescriptor = new MeshComponentPersistenceDescriptor();
            DirectionalLightDescriptor = new DirectionalLightComponentPersistenceDescriptor();
            PlaceholderModel = new AuthoringPlaceholderRuntimeModel();
            PlaceholderMaterial = new RuntimeMaterial();
            FpsDescriptor = new FPSComponentPersistenceDescriptor();
        }

        /// <summary>
        /// Creates the canonical directional-shadow plaza scene asset.
        /// </summary>
        /// <param name="planeReference">Stable generated plane model reference.</param>
        /// <param name="cubeReference">Stable generated cube model reference.</param>
        /// <param name="standardMaterialReference">Stable generated standard material reference.</param>
        /// <returns>Authored scene asset for the directional-shadow plaza showcase.</returns>
        public SceneAsset CreateSceneAsset(
            SceneAssetReference planeReference,
            SceneAssetReference cubeReference,
            SceneAssetReference standardMaterialReference) {
            if (planeReference == null) {
                throw new ArgumentNullException(nameof(planeReference));
            } else if (cubeReference == null) {
                throw new ArgumentNullException(nameof(cubeReference));
            } else if (standardMaterialReference == null) {
                throw new ArgumentNullException(nameof(standardMaterialReference));
            }

            return new SceneAsset {
                Id = SceneId,
                AssetReferences = new[] {
                    planeReference,
                    cubeReference,
                    standardMaterialReference,
                    CreateEditorFontReference()
                },
                RootEntities = new[] {
                    CreateCameraEntity(),
                    CreateDirectionalLightEntity(),
                    CreateGroundEntity(planeReference, standardMaterialReference),
                    CreateTowerEntity("directional-shadow-plaza-tower-left", "DirectionalShadowPlazaTowerLeft", new float3(-10f, 4f, -6f), new float3(3f, 8f, 3f), -0.35f, 0.18f, cubeReference, standardMaterialReference),
                    CreateTowerEntity("directional-shadow-plaza-tower-center", "DirectionalShadowPlazaTowerCenter", new float3(0f, 5f, 0f), new float3(4f, 10f, 4f), 0f, 0.11f, cubeReference, standardMaterialReference),
                    CreateTowerEntity("directional-shadow-plaza-tower-right", "DirectionalShadowPlazaTowerRight", new float3(11f, 3.5f, 7f), new float3(3f, 7f, 3f), 0.45f, -0.16f, cubeReference, standardMaterialReference),
                    CreateOrbitHeroEntity(cubeReference, standardMaterialReference),
                    CreateReceiverEntity("directional-shadow-plaza-receiver-a", "DirectionalShadowPlazaReceiverA", new float3(-18f, 0.5f, 16f), new float3(2f, 1f, 2f), cubeReference, standardMaterialReference),
                    CreateReceiverEntity("directional-shadow-plaza-receiver-b", "DirectionalShadowPlazaReceiverB", new float3(-8f, 1.5f, 19f), new float3(3f, 3f, 3f), cubeReference, standardMaterialReference),
                    CreateReceiverEntity("directional-shadow-plaza-receiver-c", "DirectionalShadowPlazaReceiverC", new float3(16f, 1f, -14f), new float3(2f, 2f, 5f), cubeReference, standardMaterialReference),
                    CreateReceiverEntity("directional-shadow-plaza-receiver-d", "DirectionalShadowPlazaReceiverD", new float3(17f, 2f, 12f), new float3(4f, 4f, 2f), cubeReference, standardMaterialReference)
                }
            };
        }

        /// <summary>
        /// Creates the authored camera entity for the showcase scene.
        /// </summary>
        /// <returns>Serialized camera entity.</returns>
        SceneEntityAsset CreateCameraEntity() {
            return new SceneEntityAsset {
                Id = "directional-shadow-plaza-camera",
                Name = "DirectionalShadowPlazaCamera",
                LocalPosition = new float3(0f, 10f, -26f),
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] {
                    CreateCameraComponentRecord(),
                    RenderingScriptComponentRecordFactory.CreateCameraOrbitRecord(1, new float3(0f, 0f, 0f), 26f, 10f, 0f, 0.12f, -0.32f),
                    CreateFpsComponentRecord()
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates the authored directional light entity for the showcase scene.
        /// </summary>
        /// <returns>Serialized directional light entity.</returns>
        SceneEntityAsset CreateDirectionalLightEntity() {
            float4 orientation;
            float4.CreateFromYawPitchRoll(0f, -0.95f, 0f, out orientation);
            return new SceneEntityAsset {
                Id = "directional-shadow-plaza-sun",
                Name = "DirectionalShadowPlazaSun",
                LocalPosition = new float3(0f, 18f, 0f),
                LocalScale = float3.One,
                LocalOrientation = orientation,
                Components = new[] {
                    CreateDirectionalLightComponentRecord(2.8f, 60f),
                    RenderingScriptComponentRecordFactory.CreateSunSweepRecord(1, -0.9f, 0.9f, -0.95f, 0.35f)
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one rotating tower entity for the showcase scene.
        /// </summary>
        /// <param name="id">Stable entity id.</param>
        /// <param name="name">Display name stored on the entity.</param>
        /// <param name="localPosition">Local position assigned to the entity.</param>
        /// <param name="localScale">Local scale assigned to the entity.</param>
        /// <param name="baseYawRadians">Base yaw applied before time-based rotation.</param>
        /// <param name="angularSpeedRadians">Angular speed in radians per second.</param>
        /// <param name="modelReference">Stable generated model reference used by the mesh payload.</param>
        /// <param name="materialReference">Stable generated material reference used by the mesh payload.</param>
        /// <returns>Serialized rotating tower entity.</returns>
        SceneEntityAsset CreateTowerEntity(
            string id,
            string name,
            float3 localPosition,
            float3 localScale,
            float baseYawRadians,
            float angularSpeedRadians,
            SceneAssetReference modelReference,
            SceneAssetReference materialReference) {
            return new SceneEntityAsset {
                Id = id,
                Name = name,
                LocalPosition = localPosition,
                LocalScale = localScale,
                LocalOrientation = float4.Identity,
                Components = new[] {
                    CreateMeshComponentRecord(modelReference, materialReference),
                    RenderingScriptComponentRecordFactory.CreateTowerSpinRecord(1, baseYawRadians, angularSpeedRadians)
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates the orbiting hero prop entity for the showcase scene.
        /// </summary>
        /// <param name="modelReference">Stable generated model reference used by the mesh payload.</param>
        /// <param name="materialReference">Stable generated material reference used by the mesh payload.</param>
        /// <returns>Serialized orbiting hero entity.</returns>
        SceneEntityAsset CreateOrbitHeroEntity(SceneAssetReference modelReference, SceneAssetReference materialReference) {
            return new SceneEntityAsset {
                Id = "directional-shadow-plaza-hero",
                Name = "DirectionalShadowPlazaHero",
                LocalPosition = new float3(0f, 3f, 12f),
                LocalScale = new float3(3f, 6f, 3f),
                LocalOrientation = float4.Identity,
                Components = new[] {
                    CreateMeshComponentRecord(modelReference, materialReference),
                    RenderingScriptComponentRecordFactory.CreateOrbitRecord(1, new float3(0f, 0f, 0f), 12f, 3f, 0.25f, 0.14f)
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates the ground receiver mesh for the showcase scene.
        /// </summary>
        /// <param name="modelReference">Stable generated model reference used by the mesh payload.</param>
        /// <param name="materialReference">Stable generated material reference used by the mesh payload.</param>
        /// <returns>Serialized ground entity.</returns>
        SceneEntityAsset CreateGroundEntity(SceneAssetReference modelReference, SceneAssetReference materialReference) {
            return new SceneEntityAsset {
                Id = "directional-shadow-plaza-ground",
                Name = "DirectionalShadowPlazaGround",
                LocalPosition = new float3(0f, -0.5f, 0f),
                LocalScale = new float3(42f, 1f, 42f),
                LocalOrientation = float4.Identity,
                Components = new[] {
                    CreateMeshComponentRecord(modelReference, materialReference)
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one passive receiver entity used to make shadow travel easy to read.
        /// </summary>
        /// <param name="id">Stable entity id.</param>
        /// <param name="name">Display name stored on the entity.</param>
        /// <param name="localPosition">Local position assigned to the entity.</param>
        /// <param name="localScale">Local scale assigned to the entity.</param>
        /// <param name="modelReference">Stable generated model reference used by the mesh payload.</param>
        /// <param name="materialReference">Stable generated material reference used by the mesh payload.</param>
        /// <returns>Serialized passive receiver entity.</returns>
        SceneEntityAsset CreateReceiverEntity(
            string id,
            string name,
            float3 localPosition,
            float3 localScale,
            SceneAssetReference modelReference,
            SceneAssetReference materialReference) {
            return new SceneEntityAsset {
                Id = id,
                Name = name,
                LocalPosition = localPosition,
                LocalScale = localScale,
                LocalOrientation = float4.Identity,
                Components = new[] {
                    CreateMeshComponentRecord(modelReference, materialReference)
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one serialized camera component record.
        /// </summary>
        /// <returns>Serialized camera component record.</returns>
        SceneComponentAssetRecord CreateCameraComponentRecord() {
            return new SceneComponentAssetRecord {
                ComponentTypeId = CameraComponentTypeId,
                ComponentIndex = 0,
                Payload = WriteCameraPayload()
            };
        }

        /// <summary>
        /// Creates one serialized FPS overlay component record for the showcase camera.
        /// </summary>
        /// <returns>Serialized FPS overlay component record.</returns>
        SceneComponentAssetRecord CreateFpsComponentRecord() {
            FPSComponent fpsComponent = new FPSComponent {
                Font = new FontAsset(new FontInfo("DirectionalShadowPlazaFpsPlaceholder", 16, 4f), null, new Dictionary<char, FontChar>(), 16f, 1, 1)
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(FontReferenceName, CreateEditorFontReference());
            return FpsDescriptor.SerializeComponent(fpsComponent, 2, saveState);
        }

        /// <summary>
        /// Builds the stable scene asset reference for the editor's built-in font.
        /// </summary>
        /// <returns>Stable generated editor-font reference.</returns>
        SceneAssetReference CreateEditorFontReference() {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "generated/editor/fonts/ui.hefont",
                ProviderId = "editor",
                AssetId = "ui-font"
            };
        }

        /// <summary>
        /// Creates one serialized mesh component record.
        /// </summary>
        /// <param name="modelReference">Stable generated model reference used by the mesh payload.</param>
        /// <param name="materialReference">Stable generated material reference used by the mesh payload.</param>
        /// <returns>Serialized mesh component record.</returns>
        SceneComponentAssetRecord CreateMeshComponentRecord(SceneAssetReference modelReference, SceneAssetReference materialReference) {
            return new SceneComponentAssetRecord {
                ComponentTypeId = MeshComponentTypeId,
                ComponentIndex = 0,
                Payload = WriteMeshPayload(modelReference, materialReference)
            };
        }

        /// <summary>
        /// Creates one serialized directional-light component record.
        /// </summary>
        /// <param name="intensity">Authored directional-light intensity.</param>
        /// <param name="shadowDistance">Authored directional-light shadow cutoff distance.</param>
        /// <returns>Serialized directional-light component record.</returns>
        SceneComponentAssetRecord CreateDirectionalLightComponentRecord(float intensity, float shadowDistance) {
            return new SceneComponentAssetRecord {
                ComponentTypeId = DirectionalLightComponentTypeId,
                ComponentIndex = 0,
                Payload = WriteDirectionalLightPayload(intensity, shadowDistance)
            };
        }

        /// <summary>
        /// Writes one serialized camera component payload.
        /// </summary>
        /// <returns>Serialized camera component payload.</returns>
        byte[] WriteCameraPayload() {
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("CameraDrawOrder", fieldWriter => fieldWriter.WriteByte(0));
            writer.WriteField("LayerMask", fieldWriter => fieldWriter.WriteUInt16(SceneObjectsLayerMask));
            writer.WriteField("Viewport", fieldWriter => fieldWriter.WriteFloat4(new float4(0f, 0f, 1280f, 720f)));
            writer.WriteField(
                "ClearSettings",
                fieldWriter => SceneComponentBinaryFieldEncoding.WriteCameraClearSettings(
                    fieldWriter,
                    new CameraClearSettings(
                        true,
                        new float4(0.06f, 0.06f, 0.09f, 1f),
                        true,
                        1f,
                        false,
                        0)));
            writer.WriteField(
                "RenderSettings",
                fieldWriter => SceneComponentBinaryFieldEncoding.WriteCameraRenderSettings(
                    fieldWriter,
                    new CameraRenderSettings {
                        DepthPrepassMode = DepthPrepassMode.Auto,
                        ShadowDistance = 60f,
                        PostProcessTier = PostProcessTier.Disabled
                    }));
            return writer.BuildPayload();
        }

        /// <summary>
        /// Writes one serialized mesh component payload.
        /// </summary>
        /// <param name="modelReference">Stable generated model reference used by the mesh.</param>
        /// <param name="materialReference">Stable generated material reference used by the mesh.</param>
        /// <returns>Serialized mesh component payload.</returns>
        byte[] WriteMeshPayload(SceneAssetReference modelReference, SceneAssetReference materialReference) {
            if (modelReference == null) {
                throw new ArgumentNullException(nameof(modelReference));
            } else if (materialReference == null) {
                throw new ArgumentNullException(nameof(materialReference));
            }

            MeshComponent meshComponent = new MeshComponent {
                Model = PlaceholderModel,
                Material = PlaceholderMaterial,
                RenderOrder3D = 0
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(MeshModelReferenceName, modelReference);
            saveState.SetAssetReference(MeshMaterialReferenceName, materialReference);
            return MeshDescriptor.SerializeComponent(meshComponent, 0, saveState).Payload;
        }

        /// <summary>
        /// Writes one serialized directional-light component payload.
        /// </summary>
        /// <param name="intensity">Authored directional-light intensity.</param>
        /// <param name="shadowDistance">Authored directional-light shadow cutoff distance.</param>
        /// <returns>Serialized directional-light component payload.</returns>
        byte[] WriteDirectionalLightPayload(float intensity, float shadowDistance) {
            DirectionalLightComponent lightComponent = new DirectionalLightComponent {
                Color = new float4(1f, 0.96f, 0.90f, 1f),
                Intensity = intensity,
                ShadowsEnabled = true,
                ShadowMapMode = ShadowMapMode.Forced,
                ShadowStrength = 1f,
                ShadowDistance = shadowDistance
            };
            return DirectionalLightDescriptor.SerializeComponent(lightComponent, 0, null).Payload;
        }
    }
}
