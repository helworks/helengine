using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies reusable binary serialization helpers for value types and scene entity references.
    /// </summary>
    public class BinarySerializationExtensionsTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the reference serialization tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes core services required by the entity reference tests.
        /// </summary>
        public BinarySerializationExtensionsTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-binary-serialization-extensions-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Deletes temporary test content after each run.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures reusable vector helpers round-trip through the binary reader and writer.
        /// </summary>
        [Fact]
        public void EngineBinaryReaderWriter_LittleEndian_RoundTripsVectorHelpers() {
            using MemoryStream stream = new MemoryStream();
            using (BinaryWriterLE writer = new BinaryWriterLE(stream)) {
                writer.WriteInt2(new int2(11, 22));
                writer.WriteInt4(new int4(33, 44, 55, 66));
                writer.WriteFloat2(new float2(1.25f, 2.5f));
                writer.WriteFloat3(new float3(3.5f, 4.5f, 5.5f));
                writer.WriteFloat4(new float4(6.5f, 7.5f, 8.5f, 9.5f));
            }

            stream.Position = 0;

            using BinaryReaderLE reader = new BinaryReaderLE(stream);
            Assert.Equal(new int2(11, 22), reader.ReadInt2());
            Assert.Equal(new int4(33, 44, 55, 66), reader.ReadInt4());
            Assert.Equal(new float2(1.25f, 2.5f), reader.ReadFloat2());
            Assert.Equal(new float3(3.5f, 4.5f, 5.5f), reader.ReadFloat3());
            Assert.Equal(new float4(6.5f, 7.5f, 8.5f, 9.5f), reader.ReadFloat4());
        }

        /// <summary>
        /// Ensures entity references serialize as stable ids and can be resolved through the shared table.
        /// </summary>
        [Fact]
        public void SceneEntityReferenceSerializer_WhenEntityIsRegistered_RoundTripsTheReferenceId() {
            EditorEntity entity = new EditorEntity {
                Name = "Target",
                LayerMask = EditorLayerMasks.SceneObjects
            };
            SceneEntityReferenceTable referenceTable = new SceneEntityReferenceTable();
            string entityId = referenceTable.GetOrCreateEntityId(entity);

            using MemoryStream stream = new MemoryStream();
            using (BinaryWriterLE writer = new BinaryWriterLE(stream)) {
                writer.WriteEntityReference(entity, referenceTable);
            }

            stream.Position = 0;

            using BinaryReaderLE reader = new BinaryReaderLE(stream);
            SceneEntityReference reference = reader.ReadSceneEntityReference();

            Assert.Equal(entityId, reference.EntityId);
            Assert.Same(entity, referenceTable.Resolve(entityId));
        }
    }
}

