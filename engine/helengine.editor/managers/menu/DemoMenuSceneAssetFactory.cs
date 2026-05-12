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
        /// Descriptor used to serialize sprite scene visuals.
        /// </summary>
        readonly SpriteComponentPersistenceDescriptor SpriteDescriptor;

        /// <summary>
        /// Descriptor used to serialize rounded rectangle scene visuals.
        /// </summary>
        readonly RoundedRectComponentPersistenceDescriptor RoundedRectDescriptor;

        /// <summary>
        /// Descriptor used to serialize the FPS overlay component.
        /// </summary>
        readonly FPSComponentPersistenceDescriptor FpsDescriptor;

        /// <summary>
        /// Descriptor used to serialize automatic reflected component payloads such as clip and scroll metadata.
        /// </summary>
        readonly AutomaticScriptComponentPersistenceDescriptor AutomaticDescriptor;

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
            SpriteDescriptor = new SpriteComponentPersistenceDescriptor();
            RoundedRectDescriptor = new RoundedRectComponentPersistenceDescriptor();
            FpsDescriptor = new FPSComponentPersistenceDescriptor();
            AutomaticDescriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
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
            if (definition.OverlayImage != null) {
                assetReferences.Add(BuildFileTextureReference(definition.OverlayImage.TexturePath));
            }

            return new SceneAsset {
                Id = sceneId,
                AssetReferences = assetReferences.ToArray(),
                SceneSettings = new SceneSettingsAsset {
                    CanvasProfile = new SceneCanvasProfile {
                        Width = DemoMenuLayout.CanvasWidth,
                        Height = DemoMenuLayout.CanvasHeight
                    }
                },
                RootEntities = new[] {
                    BuildCameraEntityAsset(definition),
                    BuildMenuRootEntityAsset(providerTypeName, definition)
                }
            };
        }

        /// <summary>
        /// Builds the serialized camera entity stored in the baked demo-disc scene.
        /// </summary>
        /// <param name="definition">Menu definition that provides the authored body-font reference.</param>
        SceneEntityAsset BuildCameraEntityAsset(MenuDefinition definition) {
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            }

            return new SceneEntityAsset {
                Id = "demo-disc-camera",
                Name = "DemoDiscCamera",
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] {
                    BuildCameraComponentRecord(),
                    CreateFpsComponentRecord(definition.BodyFontPath)
                },
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
            writer.WriteField("Viewport", fieldWriter => fieldWriter.WriteFloat4(new float4(0f, 0f, 1f, 1f)));
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
            ViewportComponent viewportComponent = new ViewportComponent {
                BindingMode = ViewportComponent.ScreenBindingMode,
                FixedSize = new int2(DemoMenuLayout.CanvasWidth, DemoMenuLayout.CanvasHeight)
            };
            ReferenceCanvasFitComponent referenceCanvasFitComponent = new ReferenceCanvasFitComponent {
                ReferenceWidth = DemoMenuLayout.CanvasWidth,
                ReferenceHeight = DemoMenuLayout.CanvasHeight
            };
            SceneComponentAssetRecord buildRecord = DemoMenuBuildDescriptor.SerializeComponent(buildComponent, 0, null);
            SceneComponentAssetRecord viewportRecord = AutomaticDescriptor.SerializeComponent(viewportComponent, 1, null);
            SceneComponentAssetRecord referenceCanvasFitRecord = AutomaticDescriptor.SerializeComponent(referenceCanvasFitComponent, 2, null);
            return new SceneEntityAsset {
                Id = "demo-disc-menu-root",
                Name = "DemoDiscMenuRoot",
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] { buildRecord, viewportRecord, referenceCanvasFitRecord },
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
            if (definition.OverlayImage != null) {
                children.Add(BuildOverlayImageEntityAsset(definition.OverlayImage));
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
            AnchorComponent panelAnchor = new AnchorComponent();
            panelAnchor.SetAnchorDistances(left: 88f, top: 190f);
            SceneComponentAssetRecord panelRecord = DemoMenuPanelDescriptor.SerializeComponent(panelComponent, 0, null);
            SceneComponentAssetRecord panelAnchorRecord = AutomaticDescriptor.SerializeComponent(panelAnchor, 1, null);
            MenuItemDefinition firstItem = ResolveFirstEnabledItem(panelDefinition);

            List<SceneEntityAsset> children = new List<SceneEntityAsset>();
            children.Add(BuildBackgroundEntityAsset($"panel-{panelDefinition.PanelId}-surface", new float3(0f, 0f, 0f), new int2(DemoMenuLayout.PanelWidth, DemoMenuLayout.PanelHeight), 18f, 3f, definition.SurfaceColor, definition.SurfaceBorderColor, 30));
            children.Add(BuildBackgroundEntityAsset($"panel-{panelDefinition.PanelId}-top-band", new float3(0f, 0f, 0f), new int2(DemoMenuLayout.PanelWidth, 18), 9f, 0f, definition.AccentColor, definition.AccentColor, 31));
            children.Add(BuildTextEntityAsset($"panel-{panelDefinition.PanelId}-heading", new float3(32f, 30f, 0.1f), panelDefinition.Heading, definition.BodyFontPath, definition.TextColor, new int2(420, 36), 41));
            children.Add(BuildSelectedDescriptionEntityAsset(panelDefinition.PanelId, new float3(32f, 410f, 0.1f), firstItem.Description, definition.BodyFontPath, definition.MutedTextColor));

            List<SceneEntityAsset> itemChildren = new List<SceneEntityAsset>();
            int itemInsertIndex = 0;
            for (int itemIndex = 0; itemIndex < panelDefinition.Items.Length; itemIndex++) {
                MenuItemDefinition itemDefinition = panelDefinition.Items[itemIndex];
                if (!itemDefinition.Enabled) {
                    continue;
                }

                itemChildren.Add(BuildItemEntityAsset(definition, panelDefinition, itemDefinition, itemInsertIndex));
                itemInsertIndex++;
            }

            SceneEntityAsset itemsRootEntity = BuildItemsRootEntityAsset(panelDefinition, itemChildren.ToArray());
            children.Add(BuildItemsViewportEntityAsset(panelDefinition, itemsRootEntity));

            return new SceneEntityAsset {
                Id = $"panel-{panelDefinition.PanelId}",
                Name = $"Panel-{panelDefinition.PanelId}",
                LocalPosition = new float3(88f, 190f, 0f),
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] { panelRecord, panelAnchorRecord },
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
                LocalPosition = new float3(0f, visibleIndex * (DemoMenuLayout.ButtonHeight + DemoMenuLayout.ButtonSpacing), 0f),
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
        /// Builds the fixed panel-local viewport that clips overflowing menu rows.
        /// </summary>
        /// <param name="panelDefinition">Panel whose item viewport should be authored.</param>
        /// <param name="itemsRootEntity">Scrolling root entity parented beneath the viewport.</param>
        /// <returns>Viewport entity that owns the clip rectangle.</returns>
        SceneEntityAsset BuildItemsViewportEntityAsset(MenuPanelDefinition panelDefinition, SceneEntityAsset itemsRootEntity) {
            if (panelDefinition == null) {
                throw new ArgumentNullException(nameof(panelDefinition));
            }
            if (itemsRootEntity == null) {
                throw new ArgumentNullException(nameof(itemsRootEntity));
            }

            ClipRectComponent clipComponent = new ClipRectComponent {
                Size = BuildItemsViewportSize(panelDefinition)
            };

            return new SceneEntityAsset {
                Id = $"panel-{panelDefinition.PanelId}-items-viewport",
                Name = $"Panel-{panelDefinition.PanelId}-ItemsViewport",
                LocalPosition = new float3(32f, 90f, 0f),
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] {
                    AutomaticDescriptor.SerializeComponent(clipComponent, 0, null)
                },
                Children = new[] { itemsRootEntity }
            };
        }

        /// <summary>
        /// Builds the scrolling item-root entity that owns the reusable row-based scroll state.
        /// </summary>
        /// <param name="panelDefinition">Panel whose visible row count should be reflected into the scroll metadata.</param>
        /// <param name="itemChildren">Baked item rows that should scroll inside the viewport.</param>
        /// <returns>Scrolling item-root entity.</returns>
        SceneEntityAsset BuildItemsRootEntityAsset(MenuPanelDefinition panelDefinition, SceneEntityAsset[] itemChildren) {
            if (panelDefinition == null) {
                throw new ArgumentNullException(nameof(panelDefinition));
            }
            if (itemChildren == null) {
                throw new ArgumentNullException(nameof(itemChildren));
            }

            ScrollComponent scrollComponent = new ScrollComponent {
                Size = BuildItemsViewportSize(panelDefinition),
                ItemCount = itemChildren.Length,
                VisibleItemCount = ResolveVisibleItemCount(panelDefinition),
                ScrollStepCount = 1,
                WheelNotchSize = 120,
                RequiresPointerInside = true
            };

            return new SceneEntityAsset {
                Id = $"panel-{panelDefinition.PanelId}-items-root",
                Name = $"Panel-{panelDefinition.PanelId}-ItemsRoot",
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] {
                    AutomaticDescriptor.SerializeComponent(scrollComponent, 0, null)
                },
                Children = itemChildren
            };
        }

        /// <summary>
        /// Builds the fixed viewport size used for one panel item list.
        /// </summary>
        /// <param name="panelDefinition">Panel whose visible row count determines the viewport height.</param>
        /// <returns>Viewport size in authored scene pixels.</returns>
        int2 BuildItemsViewportSize(MenuPanelDefinition panelDefinition) {
            int visibleItemCount = ResolveVisibleItemCount(panelDefinition);
            int viewportHeight = (visibleItemCount * DemoMenuLayout.ButtonHeight)
                + ((visibleItemCount - 1) * DemoMenuLayout.ButtonSpacing);
            if (string.Equals(panelDefinition.PanelId, "scene-select", StringComparison.Ordinal)) {
                viewportHeight += DemoMenuLayout.ButtonSpacing + (DemoMenuLayout.ButtonHeight / 2);
            }

            return new int2(DemoMenuLayout.ButtonWidth, viewportHeight);
        }

        /// <summary>
        /// Resolves the authored visible-row count for one menu panel.
        /// </summary>
        /// <param name="panelDefinition">Panel definition whose visible-row count should be validated.</param>
        /// <returns>Validated visible-row count.</returns>
        int ResolveVisibleItemCount(MenuPanelDefinition panelDefinition) {
            if (panelDefinition == null) {
                throw new ArgumentNullException(nameof(panelDefinition));
            }
            if (panelDefinition.VisibleItemCount < 1) {
                throw new InvalidOperationException($"Menu panel '{panelDefinition.PanelId}' must expose at least one visible row.");
            }

            return panelDefinition.VisibleItemCount;
        }

        /// <summary>
        /// Builds one decorative overlay image entity pinned to the top-right of the fitted menu canvas.
        /// </summary>
        /// <param name="overlayImage">Overlay image definition to bake.</param>
        /// <returns>Overlay sprite entity.</returns>
        SceneEntityAsset BuildOverlayImageEntityAsset(MenuOverlayImageDefinition overlayImage) {
            if (overlayImage == null) {
                throw new ArgumentNullException(nameof(overlayImage));
            }

            SpriteComponent spriteComponent = new SpriteComponent {
                Size = new int2(overlayImage.Width, overlayImage.Height),
                RenderOrder2D = 28,
                LayerMask = RuntimeLayerMask
            };
            AnchorComponent anchorComponent = new AnchorComponent();
            anchorComponent.SetAnchorDistances(right: overlayImage.RightMargin, top: overlayImage.TopMargin);
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(TextureAssetScenePersistenceSupport.TextureReferenceName, BuildFileTextureReference(overlayImage.TexturePath));

            return new SceneEntityAsset {
                Id = "demo-disc-overlay-image",
                Name = "DemoDiscOverlayImage",
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] {
                    SpriteDescriptor.SerializeComponent(spriteComponent, 0, saveState),
                    AutomaticDescriptor.SerializeComponent(anchorComponent, 1, null)
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Builds one baked text entity.
        /// </summary>
        SceneEntityAsset BuildTextEntityAsset(string entityId, float3 localPosition, string text, string fontPath, byte4 color, int2 size, byte renderOrder2D, AnchorComponent anchorComponent = null) {
            SceneComponentAssetRecord textRecord = SerializeTextComponent(text, fontPath, color, size, renderOrder2D);
            List<SceneComponentAssetRecord> componentRecords = new List<SceneComponentAssetRecord> { textRecord };
            if (anchorComponent != null) {
                componentRecords.Add(AutomaticDescriptor.SerializeComponent(anchorComponent, 1, null));
            }
            return new SceneEntityAsset {
                Id = entityId,
                Name = entityId,
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = componentRecords.ToArray(),
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
        /// Serializes the menu-scene FPS overlay using the authored body font reference.
        /// </summary>
        /// <param name="fontPath">Project-relative font path used by the overlay.</param>
        /// <returns>Serialized FPS overlay component record.</returns>
        SceneComponentAssetRecord CreateFpsComponentRecord(string fontPath) {
            FPSComponent fpsComponent = new FPSComponent {
                Font = PlaceholderFont
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(FontAssetScenePersistenceSupport.FontReferenceName, BuildFileFontReference(fontPath));
            return FpsDescriptor.SerializeComponent(fpsComponent, 3, saveState);
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
        /// Builds one file-backed texture reference for the supplied project-relative path.
        /// </summary>
        /// <param name="relativePath">Project-relative texture path.</param>
        /// <returns>Stable file-backed texture reference.</returns>
        SceneAssetReference BuildFileTextureReference(string relativePath) {
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
