namespace helengine.editor {
    /// <summary>
    /// Builds the baked demo-disc menu scene asset consumed by the writer and future editor rebuild entrypoints.
    /// </summary>
    public class DemoMenuSceneAssetFactory {
        /// <summary>
        /// Runtime 2D layer mask used by baked menu visuals after authored scene layers are normalized during packaging.
        /// </summary>
        const byte RuntimeLayerMask = 0b00000001;

        /// Descriptor used to serialize baked demo menu root metadata.
        /// </summary>
        readonly MenuComponentPersistenceDescriptor DemoMenuBuildDescriptor;

        /// <summary>
        /// Descriptor used to serialize baked panel metadata.
        /// </summary>
        readonly MenuPanelComponentPersistenceDescriptor DemoMenuPanelDescriptor;

        /// <summary>
        /// Descriptor used to serialize baked item metadata.
        /// </summary>
        readonly MenuItemComponentPersistenceDescriptor DemoMenuItemDescriptor;

        /// <summary>
        /// Descriptor used to serialize baked selected-description markers.
        /// </summary>
        readonly MenuSelectedDescriptionComponentPersistenceDescriptor DemoMenuSelectedDescriptionDescriptor;

        /// <summary>
        /// Descriptor used to serialize text scene visuals.
        /// </summary>
        readonly TextComponentPersistenceDescriptor TextDescriptor;

        /// <summary>
        /// Descriptor used to serialize rounded rectangle scene visuals.
        /// </summary>
        readonly RoundedRectComponentPersistenceDescriptor RoundedRectDescriptor;

        /// <summary>
        /// Dummy font asset used only to satisfy text-component serialization before real asset references are applied.
        /// </summary>
        readonly FontAsset PlaceholderFont;

        /// <summary>
        /// Initializes the factory with the persistence descriptors required for baked menu scene output.
        /// </summary>
        public DemoMenuSceneAssetFactory() {
            DemoMenuBuildDescriptor = new MenuComponentPersistenceDescriptor();
            DemoMenuPanelDescriptor = new MenuPanelComponentPersistenceDescriptor();
            DemoMenuItemDescriptor = new MenuItemComponentPersistenceDescriptor();
            DemoMenuSelectedDescriptionDescriptor = new MenuSelectedDescriptionComponentPersistenceDescriptor();
            TextDescriptor = new TextComponentPersistenceDescriptor();
            RoundedRectDescriptor = new RoundedRectComponentPersistenceDescriptor();
            PlaceholderFont = new FontAsset(
                new FontInfo("Placeholder", 16, 4f),
                new ManagedRuntimeTexture {
                    Width = 1,
                    Height = 1
                },
                new Dictionary<char, FontChar>(),
                16f,
                1,
                1);
        }

        /// <summary>
        /// Builds one baked demo-disc menu scene asset from the supplied menu definition.
        /// </summary>
        /// <param name="sceneId">Scene id assigned to the generated scene asset.</param>
        /// <param name="providerTypeName">Assembly-qualified provider type name stored on the baked menu root component.</param>
        /// <param name="definition">Menu definition that should be baked into scene entities.</param>
        /// <returns>Generated baked scene asset.</returns>
        public SceneAsset BuildSceneAsset(string sceneId, string providerTypeName, MenuDefinition definition) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }
            if (string.IsNullOrWhiteSpace(providerTypeName)) {
                throw new ArgumentException("Provider type name must be provided.", nameof(providerTypeName));
            }
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            }

            List<SceneAssetReference> assetReferences = new List<SceneAssetReference>();
            assetReferences.Add(BuildFileFontReference(definition.TitleFontPath));
            assetReferences.Add(BuildFileFontReference(definition.BodyFontPath));

            return new SceneAsset {
                Id = sceneId,
                AssetReferences = assetReferences.ToArray(),
                RootEntities = new[] {
                    BuildCameraEntityAsset(),
                    BuildMenuRootEntityAsset(providerTypeName, definition)
                }
            };
        }

        /// <summary>
        /// Builds the serialized camera entity stored in the baked demo-disc scene.
        /// </summary>
        SceneEntityAsset BuildCameraEntityAsset() {
            return new SceneEntityAsset {
                Id = "demo-disc-camera",
                Name = "DemoDiscCamera",
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] { BuildCameraComponentRecord() },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Builds the serialized camera payload used by the baked demo menu scene without instantiating live runtime camera state.
        /// </summary>
        /// <returns>Serialized camera component record.</returns>
        SceneComponentAssetRecord BuildCameraComponentRecord() {
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("CameraDrawOrder", fieldWriter => fieldWriter.WriteByte(0));
            writer.WriteField("LayerMask", fieldWriter => fieldWriter.WriteUInt16(1));
            writer.WriteField("Viewport", fieldWriter => fieldWriter.WriteFloat4(new float4(0f, 0f, DemoMenuLayout.CanvasWidth, DemoMenuLayout.CanvasHeight)));
            writer.WriteField(
                "ClearSettings",
                fieldWriter => SceneComponentBinaryFieldEncoding.WriteCameraClearSettings(
                    fieldWriter,
                    new CameraClearSettings(
                        true,
                        new float4(0.11764706f, 0.06666667f, 0.16078432f, 1f),
                        true,
                        1f,
                        true,
                        1)));
            writer.WriteField(
                "RenderSettings",
                fieldWriter => SceneComponentBinaryFieldEncoding.WriteCameraRenderSettings(
                    fieldWriter,
                    new CameraRenderSettings {
                        DepthPrepassMode = DepthPrepassMode.Auto,
                        ShadowDistance = 50f,
                        PostProcessTier = PostProcessTier.High
                    }));

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.CameraComponent",
                ComponentIndex = 0,
                Payload = writer.BuildPayload()
            };
        }

        /// <summary>
        /// Builds the serialized baked demo menu root entity and generated hierarchy.
        /// </summary>
        SceneEntityAsset BuildMenuRootEntityAsset(string providerTypeName, MenuDefinition definition) {
            MenuComponent buildComponent = new MenuComponent {
                ProviderTypeName = providerTypeName,
                InitialPanelId = definition.InitialPanelId
            };
            SceneComponentAssetRecord buildRecord = DemoMenuBuildDescriptor.SerializeComponent(buildComponent, 0, null);

            return new SceneEntityAsset {
                Id = "demo-disc-menu-root",
                Name = "DemoDiscMenuRoot",
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] { buildRecord },
                Children = new[] {
                    BuildGeneratedRootEntityAsset(definition)
                }
            };
        }

        /// <summary>
        /// Builds the generated menu subtree root entity.
        /// </summary>
        SceneEntityAsset BuildGeneratedRootEntityAsset(MenuDefinition definition) {
            List<SceneEntityAsset> children = new List<SceneEntityAsset>();
            children.Add(BuildBackgroundEntityAsset("demo-disc-menu-background", new float3(0f, 0f, 0f), new int2(DemoMenuLayout.CanvasWidth, DemoMenuLayout.CanvasHeight), 0f, 0f, definition.BackgroundColor, definition.BackgroundColor, 10));
            children.Add(BuildBackgroundEntityAsset("demo-disc-menu-accent", new float3(72f, 64f, 0f), new int2(18, 520), 9f, 0f, definition.AccentSecondaryColor, definition.AccentSecondaryColor, 20));
            if (!string.IsNullOrWhiteSpace(definition.Title)) {
                children.Add(BuildTextEntityAsset("demo-disc-menu-title", new float3(96f, 56f, 0.1f), definition.Title, definition.TitleFontPath, definition.TextColor, new int2(600, 64), 40));
            }
            if (!string.IsNullOrWhiteSpace(definition.Subtitle)) {
                children.Add(BuildTextEntityAsset("demo-disc-menu-subtitle", new float3(100f, 118f, 0.1f), definition.Subtitle, definition.BodyFontPath, definition.MutedTextColor, new int2(700, 36), 41));
            }

            for (int panelIndex = 0; panelIndex < definition.Panels.Length; panelIndex++) {
                children.Add(BuildPanelEntityAsset(definition, definition.Panels[panelIndex]));
            }

            return new SceneEntityAsset {
                Id = "demo-disc-generated-menu",
                Name = DemoMenuLayout.GeneratedRootEntityName,
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = Array.Empty<SceneComponentAssetRecord>(),
                Children = children.ToArray()
            };
        }

        /// <summary>
        /// Builds one baked panel subtree.
        /// </summary>
        SceneEntityAsset BuildPanelEntityAsset(MenuDefinition definition, MenuPanelDefinition panelDefinition) {
            MenuPanelComponent panelComponent = new MenuPanelComponent {
                PanelId = panelDefinition.PanelId
            };
            SceneComponentAssetRecord panelRecord = DemoMenuPanelDescriptor.SerializeComponent(panelComponent, 0, null);
            MenuItemDefinition firstItem = ResolveFirstEnabledItem(panelDefinition);

            List<SceneEntityAsset> children = new List<SceneEntityAsset>();
            children.Add(BuildBackgroundEntityAsset($"panel-{panelDefinition.PanelId}-surface", new float3(88f, 190f, 0f), new int2(DemoMenuLayout.PanelWidth, DemoMenuLayout.PanelHeight), 18f, 3f, definition.SurfaceColor, definition.SurfaceBorderColor, 30));
            children.Add(BuildBackgroundEntityAsset($"panel-{panelDefinition.PanelId}-accent", new float3(88f, 190f, 0f), new int2(DemoMenuLayout.PanelWidth, 18), 9f, 0f, definition.AccentColor, definition.AccentColor, 31));
            children.Add(BuildTextEntityAsset($"panel-{panelDefinition.PanelId}-heading", new float3(120f, 220f, 0.1f), panelDefinition.Heading, definition.BodyFontPath, definition.TextColor, new int2(420, 36), 41));
            children.Add(BuildTextEntityAsset($"panel-{panelDefinition.PanelId}-description", new float3(120f, 258f, 0.1f), panelDefinition.Description, definition.BodyFontPath, definition.MutedTextColor, new int2(430, 52), 41));
            children.Add(BuildSelectedDescriptionEntityAsset(panelDefinition.PanelId, new float3(120f, 600f, 0.1f), firstItem.Description, definition.BodyFontPath, definition.MutedTextColor));

            int itemInsertIndex = 0;
            for (int itemIndex = 0; itemIndex < panelDefinition.Items.Length; itemIndex++) {
                MenuItemDefinition itemDefinition = panelDefinition.Items[itemIndex];
                if (!itemDefinition.Enabled) {
                    continue;
                }

                children.Add(BuildItemEntityAsset(definition, panelDefinition, itemDefinition, itemInsertIndex));
                itemInsertIndex++;
            }

            return new SceneEntityAsset {
                Id = $"panel-{panelDefinition.PanelId}",
                Name = $"Panel-{panelDefinition.PanelId}",
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] { panelRecord },
                Children = children.ToArray()
            };
        }

        /// <summary>
        /// Builds one baked item row entity.
        /// </summary>
        SceneEntityAsset BuildItemEntityAsset(MenuDefinition definition, MenuPanelDefinition panelDefinition, MenuItemDefinition itemDefinition, int visibleIndex) {
            byte4 idleFillColor = definition.AccentSecondaryColor;
            byte4 idleBorderColor = definition.SurfaceBorderColor;
            byte4 selectedFillColor = definition.AccentColor;
            byte4 selectedBorderColor = definition.AccentColor;

            MenuItemComponent itemComponent = new MenuItemComponent {
                PanelId = panelDefinition.PanelId,
                ItemId = itemDefinition.ItemId,
                Description = itemDefinition.Description,
                ActionKind = itemDefinition.Action.Kind,
                TargetId = itemDefinition.Action.TargetId,
                IdleFillColor = idleFillColor,
                IdleBorderColor = idleBorderColor,
                SelectedFillColor = selectedFillColor,
                SelectedBorderColor = selectedBorderColor
            };
            RoundedRectComponent backgroundComponent = new RoundedRectComponent {
                Size = new int2(DemoMenuLayout.ButtonWidth, DemoMenuLayout.ButtonHeight),
                Radius = 7.2f,
                BorderThickness = 2f,
                FillColor = visibleIndex == 0 ? selectedFillColor : idleFillColor,
                BorderColor = visibleIndex == 0 ? selectedBorderColor : idleBorderColor,
                RenderOrder2D = 33,
                LayerMask = RuntimeLayerMask
            };

            SceneComponentAssetRecord itemRecord = DemoMenuItemDescriptor.SerializeComponent(itemComponent, 0, null);
            SceneComponentAssetRecord backgroundRecord = RoundedRectDescriptor.SerializeComponent(backgroundComponent, 1, null);

            return new SceneEntityAsset {
                Id = $"item-{panelDefinition.PanelId}-{itemDefinition.ItemId}",
                Name = $"Item-{itemDefinition.ItemId}",
                LocalPosition = new float3(120f, 320f + (visibleIndex * (DemoMenuLayout.ButtonHeight + DemoMenuLayout.ButtonSpacing)), 0f),
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] {
                    itemRecord,
                    backgroundRecord
                },
                Children = new[] {
                    BuildTextEntityAsset(
                        $"item-label-{itemDefinition.ItemId}",
                        new float3(20f, 12f, 0.1f),
                        itemDefinition.Label,
                        definition.BodyFontPath,
                        definition.TextColor,
                        new int2(DemoMenuLayout.ButtonWidth - 40, 24),
                        34)
                }
            };
        }

        /// <summary>
        /// Builds one marker entity that hosts the selected item description text.
        /// </summary>
        SceneEntityAsset BuildSelectedDescriptionEntityAsset(string panelId, float3 localPosition, string description, string fontPath, byte4 color) {
            MenuSelectedDescriptionComponent markerComponent = new MenuSelectedDescriptionComponent();
            SceneComponentAssetRecord markerRecord = DemoMenuSelectedDescriptionDescriptor.SerializeComponent(markerComponent, 0, null);
            SceneComponentAssetRecord textRecord = SerializeTextComponent(description, fontPath, color, new int2(500, 64), 41);

            return new SceneEntityAsset {
                Id = $"selected-description-{panelId}",
                Name = $"SelectedDescription-{panelId}",
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] {
                    markerRecord,
                    textRecord
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Builds one baked text entity.
        /// </summary>
        SceneEntityAsset BuildTextEntityAsset(string entityId, float3 localPosition, string text, string fontPath, byte4 color, int2 size, byte renderOrder2D) {
            SceneComponentAssetRecord textRecord = SerializeTextComponent(text, fontPath, color, size, renderOrder2D);
            return new SceneEntityAsset {
                Id = entityId,
                Name = entityId,
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] { textRecord },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Builds one baked rounded-rectangle visual entity.
        /// </summary>
        SceneEntityAsset BuildBackgroundEntityAsset(string entityId, float3 localPosition, int2 size, float radius, float borderThickness, byte4 fillColor, byte4 borderColor, byte renderOrder2D) {
            RoundedRectComponent roundedRectComponent = new RoundedRectComponent {
                Size = size,
                Radius = radius,
                BorderThickness = borderThickness,
                FillColor = fillColor,
                BorderColor = borderColor,
                RenderOrder2D = renderOrder2D,
                LayerMask = RuntimeLayerMask
            };
            SceneComponentAssetRecord roundedRectRecord = RoundedRectDescriptor.SerializeComponent(roundedRectComponent, 0, null);
            return new SceneEntityAsset {
                Id = entityId,
                Name = entityId,
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] { roundedRectRecord },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Serializes one baked text component using the existing text scene-persistence descriptor.
        /// </summary>
        SceneComponentAssetRecord SerializeTextComponent(string text, string fontPath, byte4 color, int2 size, byte renderOrder2D) {
            TextComponent textComponent = new TextComponent {
                Text = text ?? string.Empty,
                Font = PlaceholderFont,
                Color = color,
                Size = size,
                RenderOrder2D = renderOrder2D,
                LayerMask = RuntimeLayerMask
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(FontAssetScenePersistenceSupport.FontReferenceName, BuildFileFontReference(fontPath));
            return TextDescriptor.SerializeComponent(textComponent, 0, saveState);
        }

        /// <summary>
        /// Builds one file-backed font reference for the supplied project-relative path.
        /// </summary>
        SceneAssetReference BuildFileFontReference(string relativePath) {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = relativePath.Replace('\\', '/'),
                ProviderId = string.Empty,
                AssetId = string.Empty
            };
        }

        /// <summary>
        /// Resolves the first enabled item in one panel.
        /// </summary>
        MenuItemDefinition ResolveFirstEnabledItem(MenuPanelDefinition panelDefinition) {
            for (int itemIndex = 0; itemIndex < panelDefinition.Items.Length; itemIndex++) {
                if (panelDefinition.Items[itemIndex].Enabled) {
                    return panelDefinition.Items[itemIndex];
                }
            }

            throw new InvalidOperationException($"Menu panel '{panelDefinition.PanelId}' does not contain any enabled items.");
        }
    }
}
