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
                        SceneId = "Scenes/DemoDiscMainMenu.helen",
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
                        SceneId = "Scenes/DemoDiscMainMenu.helen",
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
            Assert.Equal("Scenes/DemoDiscMainMenu.helen", restored.Steps[1].SceneId);
            Assert.Equal("load-menu", restored.Steps[1].Label);
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
        /// Ensures unsupported reflected member types fail clearly instead of being silently skipped.
        /// </summary>
        [Fact]
        public void SerializeComponent_WhenMemberTypeIsUnsupported_Throws() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            UnsupportedScriptComponent component = new UnsupportedScriptComponent();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => descriptor.SerializeComponent(component, 0, new EntityComponentSaveState()));

            Assert.Contains(typeof(Entity).FullName, exception.Message, StringComparison.Ordinal);
        }
    }
}
