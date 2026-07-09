using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies hierarchy and inspector behavior for blueprint-inherited scene content.
    /// </summary>
    public class BlueprintEditorReadOnlyTests : IDisposable {
        readonly string TempRootPath;

        public BlueprintEditorReadOnlyTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-blueprint-editor-readonly-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
            EditorInputCaptureService.Reset();
            EditorSelectionService.ClearSelection();
            EditorKeyboardFocusService.Reset();
            EditorSceneMutationService.Reset();

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));

            CreateUiCamera(640, 480, EditorLayerMasks.SceneHierarchyContent);
        }

        public void Dispose() {
            EditorInputCaptureService.Reset();
            EditorSelectionService.ClearSelection();
            EditorKeyboardFocusService.Reset();
            EditorSceneMutationService.Reset();

            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        [Fact]
        public void RefreshHierarchy_WhenEntityIsInheritedBlueprint_AppendsBlueprintSuffixAndUsesSecondaryTextColor() {
            EditorEntity entity = new EditorEntity {
                Name = "Blueprint Child"
            };
            entity.AddComponent(new BlueprintInheritedEntityComponent {
                BlueprintAssetPath = "Blueprints/Test.hblueprint",
                SourceEntityId = 7u
            });

            SceneHierarchyPanel panel = new SceneHierarchyPanel(CreateFont());
            panel.RefreshHierarchy();

            SceneHierarchyRow row = FindVisibleRow(panel, entity);

            Assert.Equal("Blueprint Child [blueprint]", row.Label.Text);
            Assert.Equal(ThemeManager.Colors.InputForegroundSecondary, row.Label.Color);
        }

        [Fact]
        public void ShowComponents_WhenReadOnlyRequested_UsesReadOnlyRowsAndDisablesSectionRemoveButtons() {
            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(new HostFileSystemContentStreamSource(TempRootPath)));
            EditorEntity entity = CreateInheritedEntity();

            view.ShowComponents(entity, ComponentPlatformEditingService.CommonPlatformId, true);

            List<ComponentSectionView> sections = GetPrivateField<List<ComponentSectionView>>(view, "ActiveSections");
            List<ComponentPropertyRow> activeRows = GetPrivateField<List<ComponentPropertyRow>>(view, "ActiveRows");
            Assert.NotEmpty(sections);
            Assert.NotEmpty(activeRows);
            Assert.All(sections, section => Assert.False(section.RemoveButtonHost.Enabled));
            Assert.All(activeRows, row => Assert.Equal(ComponentPropertyRowKind.ReadOnly, row.Kind));
        }

        [Fact]
        public void ShowEntityProperties_WhenEntityIsInheritedBlueprint_HidesTransformEditingAndUsesReadOnlyComponentRows() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(new HostFileSystemContentStreamSource(TempRootPath)));
            EditorEntity entity = CreateInheritedEntity();

            panel.ShowEntityProperties(entity, new[] { "windows" });

            EditorEntity transformRoot = GetPrivateField<EditorEntity>(panel, "TransformRoot");
            EditorEntity addComponentButtonRoot = GetPrivateField<EditorEntity>(panel, "AddComponentButtonRoot");
            PlatformTabStripView platformTabStrip = GetPrivateField<PlatformTabStripView>(panel, "ComponentPlatformTabStrip");
            ComponentPropertiesView componentView = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
            List<ComponentPropertyRow> activeRows = GetPrivateField<List<ComponentPropertyRow>>(componentView, "ActiveRows");
            List<TextComponent> lineTexts = GetPrivateField<List<TextComponent>>(panel, "lineTexts");

            Assert.False(transformRoot.Enabled);
            Assert.False(addComponentButtonRoot.Enabled);
            Assert.False(platformTabStrip.Root.Enabled);
            Assert.NotEmpty(activeRows);
            Assert.All(activeRows, row => Assert.Equal(ComponentPropertyRowKind.ReadOnly, row.Kind));
            Assert.Contains(lineTexts, text => string.Equals(text.Text, "Inherited blueprint entity", StringComparison.Ordinal));
            Assert.Contains(lineTexts, text => string.Equals(text.Text, "This selection is read-only. Edit the source blueprint asset to change it.", StringComparison.Ordinal));
        }

        EditorEntity CreateInheritedEntity() {
            EditorEntity entity = new EditorEntity {
                Name = "Blueprint Child"
            };
            entity.AddComponent(new DirectionalLightComponent {
                Intensity = 2.5f
            });
            entity.AddComponent(new BlueprintInheritedEntityComponent {
                BlueprintAssetPath = "Blueprints/Test.hblueprint",
                SourceEntityId = 7u
            });
            return entity;
        }

        SceneHierarchyRow FindVisibleRow(SceneHierarchyPanel panel, Entity entity) {
            List<SceneHierarchyRow> rows = GetPrivateField<List<SceneHierarchyRow>>(panel, "rows");
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++) {
                SceneHierarchyRow row = rows[rowIndex];
                if (row.Entity.Enabled && ReferenceEquals(row.NodeEntity, entity)) {
                    return row;
                }
            }

            throw new InvalidOperationException("Expected the entity to have one visible hierarchy row.");
        }

        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            foreach (char character in "Blueprint Child[]abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ -.") {
                if (characters.ContainsKey(character)) {
                    continue;
                }

                characters[character] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f);
            }

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
        }

        T GetPrivateField<T>(object instance, string fieldName) {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return (T)field.GetValue(instance);
        }

        void CreateUiCamera(int width, int height, ushort layerMask) {
            EditorEntity cameraEntity = new EditorEntity {
                InternalEntity = true,
                LayerMask = layerMask
            };

            CameraComponent camera = new CameraComponent {
                LayerMask = layerMask,
                CameraDrawOrder = 255,
                Viewport = new float4(0f, 0f, width, height)
            };
            cameraEntity.AddComponent(camera);
        }
    }
}
