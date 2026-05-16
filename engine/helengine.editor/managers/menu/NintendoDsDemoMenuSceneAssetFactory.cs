namespace helengine.editor {
    /// <summary>
    /// Builds the Nintendo DS dual-screen demo-disc menu scene asset with dedicated top and bottom screen cameras.
    /// </summary>
    public sealed class NintendoDsDemoMenuSceneAssetFactory {
        /// <summary>
        /// Runtime 2D layer mask used by baked menu visuals after authored scene layers are normalized during packaging.
        /// </summary>
        const byte RuntimeLayerMask = 0b00000001;

        /// <summary>
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
        /// Descriptor used to serialize automatic reflected component payloads such as viewport, clip, and scroll metadata.
        /// </summary>
        readonly AutomaticScriptComponentPersistenceDescriptor AutomaticDescriptor;

        /// <summary>
        /// Dummy font asset used only to satisfy text-component serialization before real asset references are applied.
        /// </summary>
        readonly FontAsset PlaceholderFont;

        /// <summary>
        /// Allocates numeric entity ids while one baked scene asset is being built.
        /// </summary>
        readonly SceneEntityAssetIdAllocator SceneEntityIdAllocator;

        /// <summary>
        /// Initializes the Nintendo DS menu factory with the persistence descriptors required for baked scene output.
        /// </summary>
        public NintendoDsDemoMenuSceneAssetFactory() {
            DemoMenuBuildDescriptor = new MenuComponentPersistenceDescriptor();
            DemoMenuPanelDescriptor = new MenuPanelComponentPersistenceDescriptor();
            DemoMenuItemDescriptor = new MenuItemComponentPersistenceDescriptor();
            DemoMenuSelectedDescriptionDescriptor = new MenuSelectedDescriptionComponentPersistenceDescriptor();
            TextDescriptor = new TextComponentPersistenceDescriptor();
            SpriteDescriptor = new SpriteComponentPersistenceDescriptor();
            RoundedRectDescriptor = new RoundedRectComponentPersistenceDescriptor();
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
            SceneEntityIdAllocator = new SceneEntityAssetIdAllocator();
        }

        /// <summary>
        /// Builds one Nintendo DS dual-screen baked demo-disc menu scene asset from the supplied menu definition.
        /// </summary>
        /// <param name="sceneId">Scene id assigned to the generated scene asset.</param>
        /// <param name="providerTypeName">Assembly-qualified provider type name stored on the baked menu root component.</param>
        /// <param name="definition">Menu definition that should be baked into scene entities.</param>
        /// <returns>Generated Nintendo DS baked scene asset.</returns>
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

            SceneEntityIdAllocator.Reset();

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
                        Width = DemoMenuNintendoDsLayout.StackedCanvasWidth,
                        Height = DemoMenuNintendoDsLayout.StackedCanvasHeight
                    }
                },
                RootEntities = [
                    BuildTopCameraEntityAsset(definition),
                    BuildBottomCameraEntityAsset(providerTypeName, definition)
                ]
            };
        }

        /// <summary>
        /// Builds the top-screen camera entity that owns the logo and title presentation.
        /// </summary>
        /// <param name="definition">Menu definition that supplies the top-screen visuals.</param>
        /// <returns>Serialized top-screen camera entity.</returns>
        SceneEntityAsset BuildTopCameraEntityAsset(MenuDefinition definition) {
            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = "DemoDiscTopScreenCamera",
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = [
                    BuildCameraComponentRecord(
                        new float4(0f, 0f, 1f, 1f),
                        definition.BackgroundColor)
                ],
                Children = [
                    BuildTopScreenRootEntityAsset(definition)
                ]
            };
        }

        /// <summary>
        /// Builds the bottom-screen camera entity that owns the interactive menu list.
        /// </summary>
        /// <param name="providerTypeName">Assembly-qualified provider type name stored on the baked menu root.</param>
        /// <param name="definition">Menu definition that supplies the interactive menu content.</param>
        /// <returns>Serialized bottom-screen camera entity.</returns>
        SceneEntityAsset BuildBottomCameraEntityAsset(string providerTypeName, MenuDefinition definition) {
            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = "DemoDiscBottomScreenCamera",
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = [
                    BuildCameraComponentRecord(
                        new float4(0f, 1f, 1f, 1f),
                        definition.BackgroundColor)
                ],
                Children = [
                    BuildBottomMenuRootEntityAsset(providerTypeName, definition)
                ]
            };
        }

        /// <summary>
        /// Builds the serialized camera payload used by the Nintendo DS baked menu scene without instantiating live runtime camera state.
        /// </summary>
        /// <param name="viewport">Authored viewport expressed in normalized stacked-screen coordinates.</param>
        /// <param name="backgroundColor">Camera clear color.</param>
        /// <returns>Serialized camera component record.</returns>
        SceneComponentAssetRecord BuildCameraComponentRecord(float4 viewport, byte4 backgroundColor) {
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("CameraDrawOrder", fieldWriter => fieldWriter.WriteByte(0));
            writer.WriteField("LayerMask", fieldWriter => fieldWriter.WriteUInt16(1));
            writer.WriteField("Viewport", fieldWriter => fieldWriter.WriteFloat4(viewport));
            writer.WriteField(
                "ClearSettings",
                fieldWriter => SceneComponentBinaryFieldEncoding.WriteCameraClearSettings(
                    fieldWriter,
                    new CameraClearSettings(
                        true,
                        new float4(backgroundColor.X / 255f, backgroundColor.Y / 255f, backgroundColor.Z / 255f, backgroundColor.W / 255f),
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
        /// Builds the top-screen viewport root that hosts the branding-only content.
        /// </summary>
        /// <param name="definition">Menu definition that supplies the top-screen visuals.</param>
        /// <returns>Serialized top-screen viewport root.</returns>
        SceneEntityAsset BuildTopScreenRootEntityAsset(MenuDefinition definition) {
            ViewportComponent viewportComponent = new ViewportComponent {
                BindingMode = ViewportComponent.AncestorCameraBindingMode,
                FixedSize = new int2(DemoMenuNintendoDsLayout.ScreenWidth, DemoMenuNintendoDsLayout.ScreenHeight),
                ScalingMode = ViewportComponent.ReferenceCanvasScalingMode,
                ReferenceWidth = DemoMenuNintendoDsLayout.ScreenWidth,
                ReferenceHeight = DemoMenuNintendoDsLayout.ScreenHeight
            };

            List<SceneEntityAsset> children = new List<SceneEntityAsset>();
            if (definition.OverlayImage != null) {
                children.Add(BuildTopScreenLogoEntityAsset(definition.OverlayImage));
            }
            if (!string.IsNullOrWhiteSpace(definition.Title)) {
                children.Add(BuildTopScreenTitleEntityAsset(definition));
            }

            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = "DemoDiscTopScreenRoot",
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = [
                    AutomaticDescriptor.SerializeComponent(viewportComponent, 0, null)
                ],
                Children = children.ToArray()
            };
        }

        /// <summary>
        /// Builds the bottom-screen menu root that hosts the runtime menu component.
        /// </summary>
        /// <param name="providerTypeName">Assembly-qualified provider type name stored on the baked menu root component.</param>
        /// <param name="definition">Menu definition that should be baked into the interactive subtree.</param>
        /// <returns>Serialized bottom-screen menu root entity.</returns>
        SceneEntityAsset BuildBottomMenuRootEntityAsset(string providerTypeName, MenuDefinition definition) {
            MenuComponent buildComponent = new MenuComponent {
                ProviderTypeName = providerTypeName,
                InitialPanelId = definition.InitialPanelId
            };
            ViewportComponent viewportComponent = new ViewportComponent {
                BindingMode = ViewportComponent.AncestorCameraBindingMode,
                FixedSize = new int2(DemoMenuNintendoDsLayout.ScreenWidth, DemoMenuNintendoDsLayout.ScreenHeight),
                ScalingMode = ViewportComponent.ReferenceCanvasScalingMode,
                ReferenceWidth = DemoMenuNintendoDsLayout.ScreenWidth,
                ReferenceHeight = DemoMenuNintendoDsLayout.ScreenHeight
            };

            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = "DemoDiscMenuRoot",
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = [
                    DemoMenuBuildDescriptor.SerializeComponent(buildComponent, 0, null),
                    AutomaticDescriptor.SerializeComponent(viewportComponent, 1, null)
                ],
                Children = [
                    BuildBottomGeneratedRootEntityAsset(definition)
                ]
            };
        }

        /// <summary>
        /// Builds the generated bottom-screen menu subtree root.
        /// </summary>
        /// <param name="definition">Menu definition that should be baked into the interactive subtree.</param>
        /// <returns>Generated bottom-screen menu subtree root.</returns>
        SceneEntityAsset BuildBottomGeneratedRootEntityAsset(MenuDefinition definition) {
            List<SceneEntityAsset> children = new List<SceneEntityAsset>();
            for (int panelIndex = 0; panelIndex < definition.Panels.Length; panelIndex++) {
                children.Add(BuildBottomPanelEntityAsset(definition, definition.Panels[panelIndex]));
            }

            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = DemoMenuLayout.GeneratedRootEntityName,
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = Array.Empty<SceneComponentAssetRecord>(),
                Children = children.ToArray()
            };
        }

        /// <summary>
        /// Builds one bottom-screen menu panel subtree.
        /// </summary>
        /// <param name="definition">Menu definition that supplies the shared panel styling.</param>
        /// <param name="panelDefinition">Panel definition that should be baked.</param>
        /// <returns>Serialized bottom-screen panel subtree.</returns>
        SceneEntityAsset BuildBottomPanelEntityAsset(MenuDefinition definition, MenuPanelDefinition panelDefinition) {
            MenuPanelComponent panelComponent = new MenuPanelComponent {
                PanelId = panelDefinition.PanelId
            };

            MenuItemDefinition firstItem = ResolveFirstEnabledItem(panelDefinition);
            List<SceneEntityAsset> children = new List<SceneEntityAsset>();
            children.Add(BuildBackgroundEntityAsset(
                $"panel-{panelDefinition.PanelId}-surface",
                new float3(0f, 0f, 0f),
                new int2(DemoMenuNintendoDsLayout.PanelWidth, DemoMenuNintendoDsLayout.PanelHeight),
                10f,
                2f,
                definition.SurfaceColor,
                definition.SurfaceBorderColor,
                30));
            children.Add(BuildBackgroundEntityAsset(
                $"panel-{panelDefinition.PanelId}-top-band",
                new float3(0f, 0f, 0f),
                new int2(DemoMenuNintendoDsLayout.PanelWidth, 12),
                6f,
                0f,
                definition.AccentColor,
                definition.AccentColor,
                31));
            children.Add(BuildTextEntityAsset(
                $"panel-{panelDefinition.PanelId}-heading",
                new float3(12f, 18f, 0.1f),
                panelDefinition.Heading,
                definition.BodyFontPath,
                definition.TextColor,
                new int2(216, 16),
                41,
                0.75f));
            children.Add(BuildSelectedDescriptionEntityAsset(
                panelDefinition.PanelId,
                new float3(12f, 154f, 0.1f),
                firstItem.Description,
                definition.BodyFontPath,
                definition.MutedTextColor));

            List<SceneEntityAsset> itemChildren = new List<SceneEntityAsset>();
            int visibleIndex = 0;
            for (int itemIndex = 0; itemIndex < panelDefinition.Items.Length; itemIndex++) {
                MenuItemDefinition itemDefinition = panelDefinition.Items[itemIndex];
                if (!itemDefinition.Enabled) {
                    continue;
                }

                itemChildren.Add(BuildBottomItemEntityAsset(definition, panelDefinition, itemDefinition, visibleIndex));
                visibleIndex++;
            }

            SceneEntityAsset itemsRootEntity = BuildBottomItemsRootEntityAsset(panelDefinition, itemChildren.ToArray());
            children.Add(BuildBottomItemsViewportEntityAsset(panelDefinition, itemsRootEntity));

            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = $"Panel-{panelDefinition.PanelId}",
                LocalPosition = new float3(8f, 8f, 0f),
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = [
                    DemoMenuPanelDescriptor.SerializeComponent(panelComponent, 0, null)
                ],
                Children = children.ToArray()
            };
        }

        /// <summary>
        /// Builds one bottom-screen menu item row.
        /// </summary>
        /// <param name="definition">Menu definition that supplies shared item colors.</param>
        /// <param name="panelDefinition">Panel that owns the item.</param>
        /// <param name="itemDefinition">Item definition that should be baked.</param>
        /// <param name="visibleIndex">Visible enabled-item index inside the panel.</param>
        /// <returns>Serialized item row entity.</returns>
        SceneEntityAsset BuildBottomItemEntityAsset(MenuDefinition definition, MenuPanelDefinition panelDefinition, MenuItemDefinition itemDefinition, int visibleIndex) {
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
                Size = new int2(DemoMenuNintendoDsLayout.ButtonWidth, DemoMenuNintendoDsLayout.ButtonHeight),
                Radius = 4f,
                BorderThickness = 1f,
                FillColor = visibleIndex == 0 ? selectedFillColor : idleFillColor,
                BorderColor = visibleIndex == 0 ? selectedBorderColor : idleBorderColor,
                RenderOrder2D = 33,
                LayerMask = RuntimeLayerMask
            };

            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = $"Item-{itemDefinition.ItemId}",
                LocalPosition = new float3(
                    0f,
                    visibleIndex * (DemoMenuNintendoDsLayout.ButtonHeight + DemoMenuNintendoDsLayout.ButtonSpacing),
                    0f),
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = [
                    DemoMenuItemDescriptor.SerializeComponent(itemComponent, 0, null),
                    RoundedRectDescriptor.SerializeComponent(backgroundComponent, 1, null)
                ],
                Children = [
                    BuildTextEntityAsset(
                        $"item-label-{itemDefinition.ItemId}",
                        new float3(8f, 2f, 0.1f),
                        itemDefinition.Label,
                        definition.BodyFontPath,
                        definition.TextColor,
                        new int2(DemoMenuNintendoDsLayout.ButtonWidth - 16, 14),
                        34,
                        0.75f)
                ]
            };
        }

        /// <summary>
        /// Builds one marker entity that hosts the selected item description text.
        /// </summary>
        /// <param name="panelId">Panel id used in the generated marker entity name.</param>
        /// <param name="localPosition">Panel-local selected-description text position.</param>
        /// <param name="description">Initial selected item description.</param>
        /// <param name="fontPath">Project-relative font path.</param>
        /// <param name="color">Text color.</param>
        /// <returns>Serialized selected-description marker entity.</returns>
        SceneEntityAsset BuildSelectedDescriptionEntityAsset(string panelId, float3 localPosition, string description, string fontPath, byte4 color) {
            MenuSelectedDescriptionComponent markerComponent = new MenuSelectedDescriptionComponent();
            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = $"SelectedDescription-{panelId}",
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = [
                    DemoMenuSelectedDescriptionDescriptor.SerializeComponent(markerComponent, 0, null),
                    SerializeTextComponent(description, fontPath, color, new int2(216, 16), 41, 0.70f)
                ],
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Builds the fixed panel-local viewport that clips overflowing Nintendo DS menu rows.
        /// </summary>
        /// <param name="panelDefinition">Panel whose item viewport should be authored.</param>
        /// <param name="itemsRootEntity">Scrolling root entity parented beneath the viewport.</param>
        /// <returns>Viewport entity that owns the clip rectangle.</returns>
        SceneEntityAsset BuildBottomItemsViewportEntityAsset(MenuPanelDefinition panelDefinition, SceneEntityAsset itemsRootEntity) {
            ClipRectComponent clipComponent = new ClipRectComponent {
                Size = BuildBottomItemsViewportSize(panelDefinition)
            };

            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = $"Panel-{panelDefinition.PanelId}-ItemsViewport",
                LocalPosition = new float3(12f, 44f, 0f),
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = [
                    AutomaticDescriptor.SerializeComponent(clipComponent, 0, null)
                ],
                Children = [itemsRootEntity]
            };
        }

        /// <summary>
        /// Builds the scrolling item-root entity that owns the reusable row-based Nintendo DS scroll state.
        /// </summary>
        /// <param name="panelDefinition">Panel whose visible row count should be reflected into the scroll metadata.</param>
        /// <param name="itemChildren">Baked item rows that should scroll inside the viewport.</param>
        /// <returns>Scrolling item-root entity.</returns>
        SceneEntityAsset BuildBottomItemsRootEntityAsset(MenuPanelDefinition panelDefinition, SceneEntityAsset[] itemChildren) {
            ScrollComponent scrollComponent = new ScrollComponent {
                Size = BuildBottomItemsViewportSize(panelDefinition),
                ItemCount = itemChildren.Length,
                VisibleItemCount = ResolveVisibleItemCount(panelDefinition),
                ScrollStepCount = 1,
                WheelNotchSize = 120,
                RequiresPointerInside = true
            };

            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = $"Panel-{panelDefinition.PanelId}-ItemsRoot",
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = [
                    AutomaticDescriptor.SerializeComponent(scrollComponent, 0, null)
                ],
                Children = itemChildren
            };
        }

        /// <summary>
        /// Builds the fixed viewport size used for one Nintendo DS menu panel item list.
        /// </summary>
        /// <param name="panelDefinition">Panel whose visible row count determines the viewport height.</param>
        /// <returns>Viewport size in authored scene pixels.</returns>
        int2 BuildBottomItemsViewportSize(MenuPanelDefinition panelDefinition) {
            int visibleItemCount = ResolveVisibleItemCount(panelDefinition);
            int viewportHeight = (visibleItemCount * DemoMenuNintendoDsLayout.ButtonHeight)
                + ((visibleItemCount - 1) * DemoMenuNintendoDsLayout.ButtonSpacing);
            return new int2(DemoMenuNintendoDsLayout.ButtonWidth, viewportHeight);
        }

        /// <summary>
        /// Resolves the authored visible-row count for one Nintendo DS menu panel.
        /// </summary>
        /// <param name="panelDefinition">Panel definition whose visible-row count should be validated.</param>
        /// <returns>Validated visible-row count.</returns>
        int ResolveVisibleItemCount(MenuPanelDefinition panelDefinition) {
            if (panelDefinition.VisibleItemCount < 1) {
                throw new InvalidOperationException($"Menu panel '{panelDefinition.PanelId}' must expose at least one visible row.");
            }

            return panelDefinition.VisibleItemCount;
        }

        /// <summary>
        /// Builds the decorative top-screen logo entity using the authored overlay image.
        /// </summary>
        /// <param name="overlayImage">Overlay image definition to bake into the top screen.</param>
        /// <returns>Serialized top-screen logo entity.</returns>
        SceneEntityAsset BuildTopScreenLogoEntityAsset(MenuOverlayImageDefinition overlayImage) {
            int displayWidth = ResolveNintendoDsLogoWidth(overlayImage);
            int displayHeight = ResolveNintendoDsLogoHeight(overlayImage, displayWidth);
            SpriteComponent spriteComponent = new SpriteComponent {
                Size = new int2(displayWidth, displayHeight),
                RenderOrder2D = 20,
                LayerMask = RuntimeLayerMask
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(TextureAssetScenePersistenceSupport.TextureReferenceName, BuildFileTextureReference(overlayImage.TexturePath));

            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = "DemoDiscOverlayImage",
                LocalPosition = new float3((DemoMenuNintendoDsLayout.ScreenWidth - displayWidth) * 0.5f, 18f, 0f),
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = [
                    SpriteDescriptor.SerializeComponent(spriteComponent, 0, saveState)
                ],
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Builds the top-screen title text entity.
        /// </summary>
        /// <param name="definition">Menu definition that supplies the title font and text.</param>
        /// <returns>Serialized title text entity.</returns>
        SceneEntityAsset BuildTopScreenTitleEntityAsset(MenuDefinition definition) {
            return BuildTextEntityAsset(
                "DemoDiscTopScreenTitle",
                new float3(20f, 126f, 0.1f),
                definition.Title,
                definition.TitleFontPath,
                definition.TextColor,
                new int2(216, 28),
                21,
                0.85f);
        }

        /// <summary>
        /// Builds one baked text entity.
        /// </summary>
        /// <param name="entityId">Generated entity name.</param>
        /// <param name="localPosition">Local entity position.</param>
        /// <param name="text">Text content.</param>
        /// <param name="fontPath">Project-relative font asset path.</param>
        /// <param name="color">Text color.</param>
        /// <param name="size">Layout size.</param>
        /// <param name="renderOrder2D">2D render order.</param>
        /// <param name="fontScale">Font scale.</param>
        /// <returns>Serialized text entity.</returns>
        SceneEntityAsset BuildTextEntityAsset(string entityId, float3 localPosition, string text, string fontPath, byte4 color, int2 size, byte renderOrder2D, float fontScale) {
            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = entityId,
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = [
                    SerializeTextComponent(text, fontPath, color, size, renderOrder2D, fontScale)
                ],
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Builds one baked rounded-rectangle visual entity.
        /// </summary>
        /// <param name="entityId">Generated entity name.</param>
        /// <param name="localPosition">Local entity position.</param>
        /// <param name="size">Rounded rectangle size.</param>
        /// <param name="radius">Rounded corner radius.</param>
        /// <param name="borderThickness">Border thickness.</param>
        /// <param name="fillColor">Fill color.</param>
        /// <param name="borderColor">Border color.</param>
        /// <param name="renderOrder2D">2D render order.</param>
        /// <returns>Serialized rounded-rectangle entity.</returns>
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

            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = entityId,
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = [
                    RoundedRectDescriptor.SerializeComponent(roundedRectComponent, 0, null)
                ],
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Serializes one baked text component using the existing text scene-persistence descriptor.
        /// </summary>
        /// <param name="text">Text content.</param>
        /// <param name="fontPath">Project-relative font asset path.</param>
        /// <param name="color">Text color.</param>
        /// <param name="size">Layout size.</param>
        /// <param name="renderOrder2D">2D render order.</param>
        /// <param name="fontScale">Font scale.</param>
        /// <returns>Serialized text component record.</returns>
        SceneComponentAssetRecord SerializeTextComponent(string text, string fontPath, byte4 color, int2 size, byte renderOrder2D, float fontScale) {
            TextComponent textComponent = new TextComponent {
                Text = text ?? string.Empty,
                Font = PlaceholderFont,
                FontScale = fontScale,
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
        /// <param name="relativePath">Project-relative font path.</param>
        /// <returns>Stable file-backed font reference.</returns>
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
        /// <param name="panelDefinition">Panel definition to inspect.</param>
        /// <returns>First enabled item definition.</returns>
        MenuItemDefinition ResolveFirstEnabledItem(MenuPanelDefinition panelDefinition) {
            for (int itemIndex = 0; itemIndex < panelDefinition.Items.Length; itemIndex++) {
                if (panelDefinition.Items[itemIndex].Enabled) {
                    return panelDefinition.Items[itemIndex];
                }
            }

            throw new InvalidOperationException($"Menu panel '{panelDefinition.PanelId}' does not contain any enabled items.");
        }

        /// <summary>
        /// Resolves the display width used for the Nintendo DS top-screen logo.
        /// </summary>
        /// <param name="overlayImage">Overlay image definition to inspect.</param>
        /// <returns>Display width in authored pixels.</returns>
        int ResolveNintendoDsLogoWidth(MenuOverlayImageDefinition overlayImage) {
            double widthScale = (double)DemoMenuNintendoDsLayout.LogoMaxWidth / overlayImage.Width;
            double heightScale = (double)DemoMenuNintendoDsLayout.LogoMaxHeight / overlayImage.Height;
            double scale = Math.Min(1d, Math.Min(widthScale, heightScale));
            return Math.Max(1, (int)Math.Round(overlayImage.Width * scale));
        }

        /// <summary>
        /// Resolves the display height used for the Nintendo DS top-screen logo.
        /// </summary>
        /// <param name="overlayImage">Overlay image definition to inspect.</param>
        /// <param name="displayWidth">Resolved display width.</param>
        /// <returns>Display height in authored pixels.</returns>
        int ResolveNintendoDsLogoHeight(MenuOverlayImageDefinition overlayImage, int displayWidth) {
            double aspectRatio = (double)overlayImage.Height / overlayImage.Width;
            return Math.Max(1, (int)Math.Round(displayWidth * aspectRatio));
        }

        /// <summary>
        /// Allocates the next scene-local entity id for the baked menu scene currently being built.
        /// </summary>
        /// <returns>Next non-zero scene-local entity id.</returns>
        uint AllocateSceneEntityId() {
            return SceneEntityIdAllocator.Allocate();
        }
    }
}
