using helengine;
using helengine.editor.tests.testing;
using System.Linq;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the optional Core CPU profiling contract and the updateable records it emits.
    /// </summary>
    public sealed class RuntimeCpuProfileProbeTests {
        [Fact]
        public void Core_WithProfileSink_RecordsObjectManagerAndRenderStages() {
            RecordingSink sink = new RecordingSink(64UL);
            Core core = CreateInitializedCore(new CoreInitializationOptions { CpuProfileSink = sink });

            core.Update(1.0 / 60.0);
            core.CompleteFrameBoundary();
            core.Draw();

            Assert.Contains(sink.Stages, entry => entry.Stage == RuntimeCpuProfileStage.ObjectManagerUpdate);
            Assert.Contains(sink.Stages, entry => entry.Stage == RuntimeCpuProfileStage.RenderManager3D);
            Assert.All(sink.Stages, entry => Assert.Equal(64UL, entry.ElapsedMicroseconds));
        }

        [Fact]
        public void Core_WithoutProfileSink_RetainsNormalOperationAndDisabledOption() {
            CoreInitializationOptions options = new CoreInitializationOptions();
            Core core = CreateInitializedCore(options);

            core.Update(1.0 / 60.0);
            core.CompleteFrameBoundary();
            core.Draw();

            Assert.Null(options.CpuProfileSink);
            Assert.NotNull(core.ObjectManager);
        }

        [Fact]
        public void ObjectManager_ProfileRecordsDistinctInstancesAndTypeLegend() {
            RecordingSink sink = new RecordingSink(64UL);
            Core core = CreateInitializedCore(new CoreInitializationOptions { CpuProfileSink = sink });
            Entity firstEntity = CreateInitializedEntity(core);
            Entity secondEntity = CreateInitializedEntity(core);
            ProfileProbeComponent first = new ProfileProbeComponent();
            ProfileProbeComponent second = new ProfileProbeComponent();
            firstEntity.AddComponent(first);
            secondEntity.AddComponent(second);
            firstEntity.InitializeHierarchy();
            secondEntity.InitializeHierarchy();

            core.Update(1.0 / 60.0);

            ProfileUpdateableRecord[] rows = sink.Updateables
                .Where(entry => entry.Item is ProfileProbeComponent)
                .ToArray();
            Assert.Equal(2, rows.Length);
            Assert.All(rows, entry => Assert.Equal(64UL, entry.ElapsedMicroseconds));
            Assert.NotSame(rows[0].Item, rows[1].Item);
            Assert.Contains(sink.TypeNames, entry => entry.Name == nameof(ProfileProbeComponent));
            Assert.Contains(sink.TypeNames, entry => entry.Item is ProfileProbeComponent);
        }

        [Fact]
        public void ObjectManager_ProfileExposesMembershipChangeAndDiscardsClockRollback() {
            RecordingSink sink = new RecordingSink(64UL);
            Core core = CreateInitializedCore(new CoreInitializationOptions { CpuProfileSink = sink });
            Entity firstEntity = CreateInitializedEntity(core);
            Entity secondEntity = CreateInitializedEntity(core);
            ProfileProbeComponent first = new ProfileProbeComponent();
            ProfileProbeComponent second = new ProfileProbeComponent();
            firstEntity.AddComponent(first);
            secondEntity.AddComponent(second);
            firstEntity.InitializeHierarchy();
            secondEntity.InitializeHierarchy();

            core.Update(1.0 / 60.0);
            int firstUpdateCount = sink.Updateables.Count(entry => entry.Item is ProfileProbeComponent);

            secondEntity.RemoveComponent(second);
            sink.TimestampSequence = new[] { 100UL, 50UL };
            core.Update(1.0 / 60.0);

            int secondUpdateCount = sink.Updateables.Count - firstUpdateCount;
            Assert.Equal(2, firstUpdateCount);
            Assert.Equal(1, secondUpdateCount);
            Assert.True(sink.ClockFaultCount > 0);
            Assert.DoesNotContain(sink.Updateables, entry => entry.ElapsedMicroseconds > 1_000_000UL);
        }

        static Core CreateInitializedCore(CoreInitializationOptions options) {
            options.ContentStreamSource ??= new FakeContentStreamSource();
            Core core = new Core(options);
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), TestPlatformInfo.Shared, options);
            return core;
        }

        static Entity CreateInitializedEntity(Core core) {
            Entity entity = new Entity(core);
            entity.InitComponents();
            return entity;
        }

        sealed class ProfileProbeComponent : UpdateComponent {
        }

        sealed class RecordingSink : IRuntimeCpuProfileSink {
            readonly ulong StepMicroseconds;
            int TimestampSequenceIndex;
            ulong LastTimestamp;

            public RecordingSink(ulong stepMicroseconds) {
                StepMicroseconds = stepMicroseconds;
                TimestampSequence = System.Array.Empty<ulong>();
                Stages = new System.Collections.Generic.List<ProfileStageRecord>();
                Updateables = new System.Collections.Generic.List<ProfileUpdateableRecord>();
                TypeNames = new System.Collections.Generic.List<ProfileTypeRecord>();
            }

            public ulong[] TimestampSequence { get; set; }
            public int ClockFaultCount { get; private set; }
            public System.Collections.Generic.List<ProfileStageRecord> Stages { get; }
            public System.Collections.Generic.List<ProfileUpdateableRecord> Updateables { get; }
            public System.Collections.Generic.List<ProfileTypeRecord> TypeNames { get; }

            public ulong ReadMicroseconds() {
                if (TimestampSequenceIndex < TimestampSequence.Length) {
                    LastTimestamp = TimestampSequence[TimestampSequenceIndex++];
                    return LastTimestamp;
                }

                LastTimestamp += StepMicroseconds;
                return LastTimestamp;
            }

            public void RecordStage(RuntimeCpuProfileStage stage, ulong elapsedMicroseconds) {
                Stages.Add(new ProfileStageRecord(stage, elapsedMicroseconds));
            }

            public void RecordUpdateable(IUpdateable item, int index, uint typeHash, uint ownerSceneEntityId, ulong elapsedMicroseconds) {
                Updateables.Add(new ProfileUpdateableRecord(item, index, typeHash, ownerSceneEntityId, elapsedMicroseconds));
            }

            public void RegisterType(IUpdateable item, uint typeHash, string typeName) {
                if (!TypeNames.Any(entry => entry.TypeHash == typeHash)) {
                    TypeNames.Add(new ProfileTypeRecord(item, typeHash, typeName));
                }
            }

            public void ClockFault() {
                ClockFaultCount++;
            }
        }

        readonly record struct ProfileStageRecord(RuntimeCpuProfileStage Stage, ulong ElapsedMicroseconds);
        readonly record struct ProfileUpdateableRecord(IUpdateable Item, int Index, uint TypeHash, uint OwnerSceneEntityId, ulong ElapsedMicroseconds);
        readonly record struct ProfileTypeRecord(IUpdateable Item, uint TypeHash, string Name);
    }
}
