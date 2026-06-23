using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies the automatic reflected editor persistence fallback for scripted components.
    /// </summary>
    public sealed class AutomaticScriptComponentPersistenceDescriptorTests : IDisposable {
        /// <summary>
        /// Temporary content root used to initialize the runtime core required by entity-backed tests.
        /// </summary>
        readonly string TempContentRootPath;

        /// <summary>
        /// Initializes the runtime core required by automatic script-component persistence tests.
        /// </summary>
        public AutomaticScriptComponentPersistenceDescriptorTests() {
            TempContentRootPath = Path.Combine(Path.GetTempPath(), "helengine-automatic-script-component-persistence-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempContentRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempContentRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Disposes the runtime core and temporary content root created for the current test instance.
        /// </summary>
        public void Dispose() {
            if (Core.Instance != null) {
                Core.Instance.Dispose();
            }
            if (Directory.Exists(TempContentRootPath)) {
                Directory.Delete(TempContentRootPath, true);
            }
        }

        /// <summary>
        /// Ensures supported scripted-component members serialize through the reflected fallback and round-trip successfully without warning noise.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenScriptComponentHasSupportedMembers_UsesAutomaticFallbackWithoutWarnings() {
            ScriptComponentReflectionSchemaBuilder schemaBuilder = new ScriptComponentReflectionSchemaBuilder();
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(schemaBuilder);
            TestScriptSerializableComponent component = new TestScriptSerializableComponent {
                DisplayName = "Menu Row",
                Visible = true,
                SortOrder = 7
            };
            List<LogEntry> warnings = new List<LogEntry>();
            Logger.WarningLogged += warnings.Add;

            try {
                SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, new EntityComponentSaveState());
                TestScriptSerializableComponent deserialized = Assert.IsType<TestScriptSerializableComponent>(
                    descriptor.DeserializeComponent(record, null, null));

                Assert.Equal(AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(TestScriptSerializableComponent)), record.ComponentTypeId);
                Assert.Equal("Menu Row", deserialized.DisplayName);
                Assert.True(deserialized.Visible);
                Assert.Equal(7, deserialized.SortOrder);
                Assert.Empty(warnings);
            } finally {
                Logger.WarningLogged -= warnings.Add;
            }
        }

        /// <summary>
        /// Ensures reflected automatic persistence can round-trip arrays of simple authored classes, enums, and double values used by the planned memory probe.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenScriptComponentContainsStepArray_RoundTripsDoubleEnumAndNestedObjects() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            TestSceneMemoryProbeSerializableComponent component = new TestSceneMemoryProbeSerializableComponent {
                ProbeName = "menu-soak",
                Loop = true,
                Steps = new[] {
                    new TestSceneMemoryProbeSerializableStep {
                        ActionKind = TestSceneMemoryProbeSerializableActionKind.Wait,
                        SceneId = "Scenes/MainMenuScene.helen",
                        DurationSeconds = 5.0d,
                        Label = "idle-menu"
                    },
                    new TestSceneMemoryProbeSerializableStep {
                        ActionKind = TestSceneMemoryProbeSerializableActionKind.LoadSceneSingle,
                        SceneId = "Scenes/AxisTest.helen",
                        DurationSeconds = 0d,
                        Label = "load-axis"
                    }
                }
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, new EntityComponentSaveState());
            TestSceneMemoryProbeSerializableComponent restored = Assert.IsType<TestSceneMemoryProbeSerializableComponent>(
                descriptor.DeserializeComponent(record, null, null));

            Assert.Equal("menu-soak", restored.ProbeName);
            Assert.True(restored.Loop);
            Assert.Equal(2, restored.Steps.Length);
            Assert.Equal(TestSceneMemoryProbeSerializableActionKind.Wait, restored.Steps[0].ActionKind);
            Assert.Equal(5.0d, restored.Steps[0].DurationSeconds);
            Assert.Equal("load-axis", restored.Steps[1].Label);
        }

        /// <summary>
        /// Ensures the real scene-memory probe runtime component round-trips through the automatic reflected persistence path.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenSceneMemoryProbeComponentUsesStepArray_RoundTripsThroughAutomaticPersistence() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            SceneMemoryProbeComponent component = new SceneMemoryProbeComponent {
                ProbeName = "menu-memory-probe",
                Loop = true,
                StartAutomatically = true,
                InitialDelaySeconds = 2.0d,
                Steps = new[] {
                    new SceneMemoryProbeStep {
                        ActionKind = SceneMemoryProbeActionKind.Wait,
                        SceneId = string.Empty,
                        DurationSeconds = 5.0d,
                        Label = "idle-menu"
                    },
                    new SceneMemoryProbeStep {
                        ActionKind = SceneMemoryProbeActionKind.LoadSceneSingle,
                        SceneId = "Scenes/MainMenuScene.helen",
                        DurationSeconds = 0d,
                        Label = "load-menu"
                    }
                }
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, new EntityComponentSaveState());
            SceneMemoryProbeComponent restored = Assert.IsType<SceneMemoryProbeComponent>(
                descriptor.DeserializeComponent(record, null, null));

            Assert.Equal("menu-memory-probe", restored.ProbeName);
            Assert.True(restored.Loop);
            Assert.True(restored.StartAutomatically);
            Assert.Equal(2.0d, restored.InitialDelaySeconds);
            Assert.Equal(2, restored.Steps.Length);
            Assert.Equal(SceneMemoryProbeActionKind.Wait, restored.Steps[0].ActionKind);
            Assert.Equal(5.0d, restored.Steps[0].DurationSeconds);
            Assert.Equal("Scenes/MainMenuScene.helen", restored.Steps[1].SceneId);
            Assert.Equal("load-menu", restored.Steps[1].Label);
        }

        /// <summary>
        /// Ensures reflected automatic persistence can round-trip string-keyed dictionaries and writes a deterministic payload regardless of insertion order.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenScriptComponentContainsStringDictionary_RoundTripsDictionaryEntriesInDeterministicKeyOrder() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            TestDictionaryScriptComponent firstComponent = new TestDictionaryScriptComponent();
            firstComponent.Labels.Add("OptionsMenu", "OptionsMenuScene");
            firstComponent.Labels.Add("MainMenu", "MainMenuScene");
            TestDictionaryScriptComponent secondComponent = new TestDictionaryScriptComponent();
            secondComponent.Labels.Add("MainMenu", "MainMenuScene");
            secondComponent.Labels.Add("OptionsMenu", "OptionsMenuScene");

            SceneComponentAssetRecord firstRecord = descriptor.SerializeComponent(firstComponent, 0, new EntityComponentSaveState());
            SceneComponentAssetRecord secondRecord = descriptor.SerializeComponent(secondComponent, 0, new EntityComponentSaveState());
            TestDictionaryScriptComponent loaded = Assert.IsType<TestDictionaryScriptComponent>(descriptor.DeserializeComponent(firstRecord, null, null));

            Assert.Equal(firstRecord.Payload, secondRecord.Payload);
            Assert.Equal("MainMenuScene", loaded.Labels["MainMenu"]);
            Assert.Equal("OptionsMenuScene", loaded.Labels["OptionsMenu"]);
        }

        /// <summary>
        /// Ensures reflected automatic persistence can round-trip dictionaries with supported integer and enum key types.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenScriptComponentContainsEnumAndIntegerKeyDictionaries_RoundTripsSupportedKeys() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            TestDictionaryKeyScriptComponent component = new TestDictionaryKeyScriptComponent();
            component.IntegerLabels.Add(7, "Seven");
            component.ModeLabels.Add(TestDictionaryMode.Secondary, "SecondaryScene");

            SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, new EntityComponentSaveState());
            TestDictionaryKeyScriptComponent loaded = Assert.IsType<TestDictionaryKeyScriptComponent>(descriptor.DeserializeComponent(record, null, null));

            Assert.Equal("Seven", loaded.IntegerLabels[7]);
            Assert.Equal("SecondaryScene", loaded.ModeLabels[TestDictionaryMode.Secondary]);
        }

        /// <summary>
        /// Ensures unsupported dictionary key types fail clearly instead of silently producing one unstable payload contract.
        /// </summary>
        [Fact]
        public void SerializeComponent_WhenScriptComponentContainsUnsupportedDictionaryKey_ThrowsInvalidOperationException() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            TestUnsupportedDictionaryKeyScriptComponent component = new TestUnsupportedDictionaryKeyScriptComponent();
            component.InvalidKeys.Add(new float2(1f, 2f), "Bad");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => descriptor.SerializeComponent(component, 0, new EntityComponentSaveState()));

            Assert.Contains("dictionary", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures dictionary payloads reject duplicate keys instead of overwriting one authored entry silently.
        /// </summary>
        [Fact]
        public void DeserializeComponent_WhenDictionaryPayloadContainsDuplicateKeys_ThrowsInvalidOperationException() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(TestDictionaryScriptComponent)),
                ComponentIndex = 0,
                Payload = BuildDuplicateDictionaryPayload()
            };

            Assert.Throws<InvalidOperationException>(() => descriptor.DeserializeComponent(record, null, null));
        }

        /// <summary>
        /// Ensures engine-owned text components now use the same automatic reflected persistence path and retain authored text layout state and font references.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenTextComponentUsesFontScaleAndAlignment_RoundTripsThroughAutomaticPersistence() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            SceneAssetReference fontReference = BuildFontReference("Fonts/Ui.hefont", "fonts", "ui");
            TextComponent component = new TextComponent {
                Font = CreateFont("Ui"),
                Text = "Scaled heading",
                WrapText = true,
                Size = new int2(512, 128),
                Color = new byte4(9, 18, 27, 255),
                SourceRect = new float4(0.05f, 0.1f, 0.9f, 0.8f),
                Rotation = 0.25f,
                FontScale = 2.0f,
                RenderOrder2D = 22,
                LayerMask = 3,
                SelectionEnabled = true,
                Texture = new TestRuntimeTexture()
            };
            System.Reflection.PropertyInfo alignmentProperty = typeof(TextComponent).GetProperty("Alignment");
            Assert.NotNull(alignmentProperty);
            alignmentProperty.SetValue(component, Enum.Parse(alignmentProperty.PropertyType, "Right"));
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(nameof(TextComponent.Font), fontReference);

            SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, saveState);

            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            FontAsset loadedFont = CreateFont("LoadedUi");
            resolver.RegisterFont(fontReference, loadedFont);
            EntitySaveComponent loadedSaveComponent = new EntitySaveComponent();

            TextComponent restored = Assert.IsType<TextComponent>(descriptor.DeserializeComponent(record, loadedSaveComponent, resolver));

            Assert.Same(loadedFont, restored.Font);
            Assert.Equal("Scaled heading", restored.Text);
            Assert.True(restored.WrapText);
            Assert.Equal(new int2(512, 128), restored.Size);
            Assert.Equal(new byte4(9, 18, 27, 255), restored.Color);
            Assert.Equal(new float4(0.05f, 0.1f, 0.9f, 0.8f), restored.SourceRect);
            Assert.Equal(0.25f, restored.Rotation);
            Assert.Equal(2.0f, restored.FontScale);
            Assert.Equal("Right", alignmentProperty.GetValue(restored)?.ToString());
            Assert.Equal((byte)22, restored.RenderOrder2D);
            Assert.Equal((byte)3, restored.LayerMask);
            Assert.True(restored.SelectionEnabled);
            Assert.Null(restored.Texture);
            Assert.True(loadedSaveComponent.TryGetComponentState(restored, out EntityComponentSaveState loadedSaveState));
            Assert.True(loadedSaveState.TryGetAssetReference(nameof(TextComponent.Font), out SceneAssetReference loadedReference));
            Assert.Equal(fontReference.RelativePath, loadedReference.RelativePath);
            Assert.Equal(fontReference.ProviderId, loadedReference.ProviderId);
            Assert.Equal(fontReference.AssetId, loadedReference.AssetId);
        }

        /// <summary>
        /// Ensures automatic reflected persistence preserves the authored build-time sprite conversion flag on engine-owned text components.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenTextComponentUsesBuildTimeSpriteConversion_RoundTripsThroughAutomaticPersistence() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            SceneAssetReference fontReference = BuildFontReference("Fonts/Demo.hefont", "fonts", "demo");
            TextComponent component = new TextComponent {
                Font = CreateFont("Demo"),
                Text = "Bake me",
                Size = new int2(128, 32),
                ConvertTextToSprite = true
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(nameof(TextComponent.Font), fontReference);

            SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, saveState);

            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            FontAsset loadedFont = CreateFont("LoadedDemo");
            resolver.RegisterFont(fontReference, loadedFont);
            EntitySaveComponent loadedSaveComponent = new EntitySaveComponent();

            TextComponent restored = Assert.IsType<TextComponent>(descriptor.DeserializeComponent(record, loadedSaveComponent, resolver));

            Assert.Same(loadedFont, restored.Font);
            Assert.True(restored.ConvertTextToSprite);
        }

        /// <summary>
        /// Ensures automatic reflected persistence restores animation-clip asset members through the same scene-reference flow used by other asset-backed scripted components.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenScriptComponentUsesAnimationClipAsset_RestoresResolvedClipAndSaveState() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            SceneAssetReference clipReference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = "Animations/Test.hanim",
                ProviderId = string.Empty,
                AssetId = string.Empty
            };
            TestAnimationClipAssetScriptComponent component = new TestAnimationClipAssetScriptComponent {
                Label = "logo",
                IdleClip = CreateAnimationClip("Animations/Preview.hanim")
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(nameof(TestAnimationClipAssetScriptComponent.IdleClip), clipReference);

            SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, saveState);

            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            AnimationClipAsset loadedClip = CreateAnimationClip("Animations/Test.hanim");
            resolver.RegisterAnimationClip(clipReference, loadedClip);
            EntitySaveComponent loadedSaveComponent = new EntitySaveComponent();

            TestAnimationClipAssetScriptComponent restored = Assert.IsType<TestAnimationClipAssetScriptComponent>(
                descriptor.DeserializeComponent(record, loadedSaveComponent, resolver));

            Assert.Equal("logo", restored.Label);
            Assert.Same(loadedClip, restored.IdleClip);
            Assert.True(loadedSaveComponent.TryGetComponentState(restored, out EntityComponentSaveState loadedSaveState));
            Assert.True(loadedSaveState.TryGetAssetReference(nameof(TestAnimationClipAssetScriptComponent.IdleClip), out SceneAssetReference loadedReference));
            Assert.Equal(clipReference.RelativePath, loadedReference.RelativePath);
            Assert.Equal(clipReference.ProviderId, loadedReference.ProviderId);
            Assert.Equal(clipReference.AssetId, loadedReference.AssetId);
        }

        /// <summary>
        /// Ensures the automatic reflected fallback still understands legacy tagged `TextComponent` payloads that persisted the font under `FontReference`.
        /// </summary>
        [Fact]
        public void DeserializeComponent_WhenLegacyTextComponentPayloadUsesFontReference_RestoresFontAndSaveState() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            SceneAssetReference fontReference = BuildFontReference("Fonts/Legacy.hefont", "fonts", "legacy");
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(TextComponent)),
                ComponentIndex = 0,
                Payload = BuildLegacyTextComponentPayload(fontReference)
            };
            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            FontAsset loadedFont = CreateFont("LoadedLegacy");
            resolver.RegisterFont(fontReference, loadedFont);
            EntitySaveComponent loadedSaveComponent = new EntitySaveComponent();

            TextComponent restored = Assert.IsType<TextComponent>(descriptor.DeserializeComponent(record, loadedSaveComponent, resolver));

            Assert.Same(loadedFont, restored.Font);
            Assert.Equal("Legacy", restored.Text);
            Assert.True(loadedSaveComponent.TryGetComponentState(restored, out EntityComponentSaveState loadedSaveState));
            Assert.True(loadedSaveState.TryGetAssetReference(nameof(TextComponent.Font), out SceneAssetReference loadedReference));
            Assert.Equal(fontReference.RelativePath, loadedReference.RelativePath);
            Assert.Equal(fontReference.ProviderId, loadedReference.ProviderId);
            Assert.Equal(fontReference.AssetId, loadedReference.AssetId);
        }

        /// <summary>
        /// Ensures runtime-only scroll bindings are ignored while the remaining reflected members still round-trip.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenScrollComponentHasIgnoredEntityMember_SkipsRuntimeOnlyReference() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            ScrollComponent component = new ScrollComponent {
                Size = new int2(320, 180),
                ItemCount = 12,
                ItemExtent = 24,
                VisibleItemCount = 4,
                ScrollStepCount = 2,
                WheelNotchSize = 120,
                RequiresPointerInside = false
            };
            component.ContentRoot = new EditorEntity();

            SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, new EntityComponentSaveState());
            ScrollComponent deserialized = Assert.IsType<ScrollComponent>(descriptor.DeserializeComponent(record, null, null));

            Assert.Equal(new int2(320, 180), deserialized.Size);
            Assert.Equal(12, deserialized.ItemCount);
            Assert.Equal(24, deserialized.ItemExtent);
            Assert.Equal(4, deserialized.VisibleItemCount);
            Assert.Equal(2, deserialized.ScrollStepCount);
            Assert.Equal(120, deserialized.WheelNotchSize);
            Assert.False(deserialized.RequiresPointerInside);
            Assert.Null(deserialized.ContentRoot);
        }

        /// <summary>
        /// Ensures empty automatic-script payloads materialize default component instances instead of failing editor deserialization.
        /// </summary>
        [Fact]
        public void DeserializeComponent_WhenAutomaticScriptPayloadIsEmpty_ReturnsDefaultComponentState() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(TestUpdateOnlyScriptComponent)),
                ComponentIndex = 0,
                Payload = Array.Empty<byte>()
            };

            TestUpdateOnlyScriptComponent component = Assert.IsType<TestUpdateOnlyScriptComponent>(descriptor.DeserializeComponent(record, null, null));

            Assert.Equal(0, component.UpdateOrder);
        }

        /// <summary>
        /// Ensures legacy scenes persisted with the old anchor component id now materialize the renamed layout component.
        /// </summary>
        [Fact]
        public void DeserializeComponent_WhenLegacyAnchorComponentTypeIdIsUsed_ReturnsLayoutComponent() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.AnchorComponent",
                ComponentIndex = 0,
                Payload = Array.Empty<byte>()
            };

            LayoutComponent component = Assert.IsType<LayoutComponent>(descriptor.DeserializeComponent(record, null, null));

            Assert.Equal(0, component.AnchorFlags);
        }

        /// <summary>
        /// Ensures unsupported reflected member types fail clearly instead of being silently skipped.
        /// </summary>
        [Fact]
        public void SerializeComponent_WhenMemberTypeIsUnsupported_Throws() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            UnsupportedScriptComponent component = new UnsupportedScriptComponent();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => descriptor.SerializeComponent(component, 0, new EntityComponentSaveState()));

            Assert.Contains(typeof(Entity).FullName, exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Creates one deterministic runtime font used by automatic persistence tests that restore asset-backed members.
        /// </summary>
        /// <param name="name">Friendly font name.</param>
        /// <returns>Runtime font asset with stable metrics and atlas shape.</returns>
        static FontAsset CreateFont(string name) {
            return new FontAsset(
                new FontInfo(name, 16, 4f),
                new TestRuntimeTexture {
                    Width = 1,
                    Height = 1
                },
                new Dictionary<char, FontChar>(),
                16f,
                1,
                1);
        }

        /// <summary>
        /// Creates one deterministic animation clip used by automatic persistence tests that restore clip asset references.
        /// </summary>
        /// <param name="assetId">Stable asset id assigned to the clip.</param>
        /// <returns>Animation clip with deterministic transform tracks.</returns>
        static AnimationClipAsset CreateAnimationClip(string assetId) {
            return new AnimationClipAsset {
                Id = assetId,
                Duration = 1f,
                PositionOffsetTracks = [
                    new PositionOffsetKeyframeTrackAsset {
                        Keyframes = [
                            new PositionKeyframeAsset(0f, float3.Zero, AnimationInterpolationMode.Linear),
                            new PositionKeyframeAsset(1f, new float3(4f, 6f, 0f), AnimationInterpolationMode.Linear)
                        ]
                    }
                ],
                ScaleTracks = [
                    new ScaleKeyframeTrackAsset {
                        Keyframes = [
                            new PositionKeyframeAsset(0f, new float3(1f, 1f, 1f), AnimationInterpolationMode.Linear),
                            new PositionKeyframeAsset(1f, new float3(1.1f, 0.95f, 1f), AnimationInterpolationMode.Linear)
                        ]
                    }
                ]
            };
        }

        /// <summary>
        /// Builds one deterministic scene asset reference for a font used by automatic persistence tests.
        /// </summary>
        /// <param name="relativePath">Project-relative path recorded for the font.</param>
        /// <param name="providerId">Generated provider identifier.</param>
        /// <param name="assetId">Provider-local asset identifier.</param>
        /// <returns>Stable scene asset reference.</returns>
        static SceneAssetReference BuildFontReference(string relativePath, string providerId, string assetId) {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = relativePath,
                ProviderId = providerId,
                AssetId = assetId
            };
        }

        /// <summary>
        /// Builds one legacy tagged `TextComponent` payload that still uses the removed `FontReference` field name.
        /// </summary>
        /// <param name="fontReference">Font reference stored under the legacy field name.</param>
        /// <returns>Serialized legacy tagged payload.</returns>
        static byte[] BuildLegacyTextComponentPayload(SceneAssetReference fontReference) {
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("FontReference", fieldWriter => {
                fieldWriter.WriteByte(1);
                fieldWriter.WriteInt32((int)fontReference.SourceKind);
                fieldWriter.WriteString(fontReference.RelativePath);
                fieldWriter.WriteString(fontReference.ProviderId);
                fieldWriter.WriteString(fontReference.AssetId);
            });
            writer.WriteField(nameof(TextComponent.Text), fieldWriter => {
                fieldWriter.WriteString("Legacy");
            });
            writer.WriteField(nameof(TextComponent.WrapText), fieldWriter => {
                fieldWriter.WriteByte(1);
            });
            writer.WriteField(nameof(TextComponent.Size), fieldWriter => {
                fieldWriter.WriteInt2(new int2(128, 32));
            });

            return writer.BuildPayload();
        }

        /// <summary>
        /// Builds one tagged editor payload for the duplicate-key dictionary regression case.
        /// </summary>
        /// <returns>Tagged payload containing two dictionary entries with the same key.</returns>
        static byte[] BuildDuplicateDictionaryPayload() {
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField(nameof(TestDictionaryScriptComponent.Labels), fieldWriter => {
                fieldWriter.WriteInt32(2);
                fieldWriter.WriteString("MainMenu");
                fieldWriter.WriteString("MainMenuScene");
                fieldWriter.WriteString("MainMenu");
                fieldWriter.WriteString("DuplicateScene");
            });

            return writer.BuildPayload();
        }
    }
}
