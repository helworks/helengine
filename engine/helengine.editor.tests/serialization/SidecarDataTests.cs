using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization {
    /// <summary>
    /// Verifies the generic keyed data block that every source import-settings sidecar carries: the in-memory store, the shared binary
    /// block helper and the typed entry registry.
    /// </summary>
    public sealed class SidecarDataTests {
        /// <summary>
        /// Ensures raw entries are listed in ordinal key order whatever order they were added in, and that stored bytes are copies.
        /// </summary>
        [Fact]
        public void SetRaw_ListsKeysInOrdinalOrderAndCopiesPayloads() {
            SidecarData data = new SidecarData();
            byte[] payload = new byte[] { 1, 2, 3 };
            data.SetRaw("b.key", payload);
            data.SetRaw("a.key", new byte[] { 4 });
            data.SetRaw("B.key", new byte[0]);
            payload[0] = 99;

            Assert.Equal(new[] { "B.key", "a.key", "b.key" }, data.Keys);
            Assert.True(data.TryGetRaw("b.key", out byte[] stored));
            Assert.Equal(new byte[] { 1, 2, 3 }, stored);
            stored[0] = 42;
            Assert.True(data.TryGetRaw("b.key", out byte[] storedAgain));
            Assert.Equal(1, storedAgain[0]);
            Assert.False(data.TryGetRaw("missing", out _));
            Assert.True(data.Remove("a.key"));
            Assert.Equal(2, data.Count);
        }

        /// <summary>
        /// Ensures a blank key or a null payload is rejected instead of being stored.
        /// </summary>
        [Fact]
        public void SetRaw_RejectsBlankKeyAndNullPayload() {
            SidecarData data = new SidecarData();

            Assert.Throws<ArgumentException>(() => data.SetRaw(" ", new byte[0]));
            Assert.Throws<ArgumentNullException>(() => data.SetRaw("key", null));
        }

        /// <summary>
        /// Ensures the block bytes do not depend on insertion order and read back to the same entries.
        /// </summary>
        [Fact]
        public void Block_WriteIsDeterministicAndRoundTrips() {
            SidecarData first = new SidecarData();
            first.SetRaw("zeta", new byte[] { 9, 8 });
            first.SetRaw("alpha", new byte[] { 1 });
            SidecarData second = new SidecarData();
            second.SetRaw("alpha", new byte[] { 1 });
            second.SetRaw("zeta", new byte[] { 9, 8 });

            byte[] firstBytes = WriteBlock(first);
            byte[] secondBytes = WriteBlock(second);
            SidecarData restored = ReadBlock(firstBytes);

            Assert.Equal(firstBytes, secondBytes);
            Assert.Equal(new[] { "alpha", "zeta" }, restored.Keys);
            Assert.True(restored.TryGetRaw("zeta", out byte[] zeta));
            Assert.Equal(new byte[] { 9, 8 }, zeta);
        }

        /// <summary>
        /// Ensures a block whose keys are duplicated, blank or out of ordinal order is rejected, as is a negative count or length.
        /// </summary>
        [Fact]
        public void Block_RejectsMalformedEntries() {
            Assert.Throws<InvalidOperationException>(() => ReadBlock(BuildBlock(-1)));
            Assert.Throws<InvalidOperationException>(() => ReadBlock(BuildBlock(2, "a", new byte[0], "a", new byte[0])));
            Assert.Throws<InvalidOperationException>(() => ReadBlock(BuildBlock(2, "b", new byte[0], "a", new byte[0])));
            Assert.Throws<InvalidOperationException>(() => ReadBlock(BuildBlock(1, "", new byte[0])));
            Assert.Throws<InvalidOperationException>(() => ReadBlock(BuildBlock(1, "a", null)));
        }

        /// <summary>
        /// Ensures a registered entry is written with its owner's version byte first and reads back as the typed value.
        /// </summary>
        [Fact]
        public void Registry_WritesOwnerVersionByteAndReadsTypedValue() {
            SidecarDataEntryRegistry registry = new SidecarDataEntryRegistry();
            registry.RegisterDefinition(new TestSidecarEntryDefinition());
            SidecarData data = new SidecarData();

            registry.Write(data, TestSidecarEntryDefinition.KeyValue, new TestSidecarEntry { Text = "hello" });

            Assert.True(data.TryGetRaw(TestSidecarEntryDefinition.KeyValue, out byte[] payload));
            Assert.Equal(TestSidecarEntryDefinition.Version, payload[0]);
            Assert.True(registry.TryRead(data, TestSidecarEntryDefinition.KeyValue, out TestSidecarEntry entry));
            Assert.Equal("hello", entry.Text);
            Assert.False(registry.TryRead(new SidecarData(), TestSidecarEntryDefinition.KeyValue, out TestSidecarEntry missing));
            Assert.Null(missing);
        }

        /// <summary>
        /// Ensures a payload with trailing bytes or an unsupported owner version fails as invalid data naming the key.
        /// </summary>
        [Fact]
        public void Registry_RejectsUnreadablePayloads() {
            SidecarDataEntryRegistry registry = new SidecarDataEntryRegistry();
            registry.RegisterDefinition(new TestSidecarEntryDefinition());
            SidecarData data = new SidecarData();
            registry.Write(data, TestSidecarEntryDefinition.KeyValue, new TestSidecarEntry { Text = "x" });
            data.TryGetRaw(TestSidecarEntryDefinition.KeyValue, out byte[] payload);

            byte[] trailing = payload.Concat(new byte[] { 0 }).ToArray();
            data.SetRaw(TestSidecarEntryDefinition.KeyValue, trailing);
            InvalidDataException trailingException = Assert.Throws<InvalidDataException>(() => registry.TryRead(data, TestSidecarEntryDefinition.KeyValue, out TestSidecarEntry _));
            Assert.Contains(TestSidecarEntryDefinition.KeyValue, trailingException.Message, StringComparison.Ordinal);

            payload[0] = 77;
            data.SetRaw(TestSidecarEntryDefinition.KeyValue, payload);
            Assert.Throws<InvalidDataException>(() => registry.TryRead(data, TestSidecarEntryDefinition.KeyValue, out TestSidecarEntry _));
        }

        /// <summary>
        /// Ensures typed access needs a registered owner, and a key or a mismatched value type cannot be registered or used twice.
        /// </summary>
        [Fact]
        public void Registry_RejectsUnregisteredKeysDuplicatesAndWrongTypes() {
            SidecarDataEntryRegistry registry = new SidecarDataEntryRegistry();
            registry.RegisterDefinition(new TestSidecarEntryDefinition());

            Assert.Throws<InvalidOperationException>(() => registry.RegisterDefinition(new TestSidecarEntryDefinition()));
            Assert.Throws<InvalidOperationException>(() => registry.Write(new SidecarData(), "plugin.unknown", new TestSidecarEntry()));
            Assert.Throws<InvalidOperationException>(() => registry.Write(new SidecarData(), TestSidecarEntryDefinition.KeyValue, "not an entry"));
            Assert.False(registry.IsRegistered("plugin.unknown"));
        }

        /// <summary>
        /// Writes a block through the shared helper.
        /// </summary>
        /// <param name="data">Data to write.</param>
        /// <returns>Encoded block bytes.</returns>
        static byte[] WriteBlock(SidecarData data) {
            using MemoryStream stream = new MemoryStream();
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian)) {
                SidecarDataBinarySerializer.Write(writer, data);
            }

            return stream.ToArray();
        }

        /// <summary>
        /// Reads a block through the shared helper.
        /// </summary>
        /// <param name="bytes">Encoded block bytes.</param>
        /// <returns>Decoded data.</returns>
        static SidecarData ReadBlock(byte[] bytes) {
            using MemoryStream stream = new MemoryStream(bytes);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            return SidecarDataBinarySerializer.Read(reader);
        }

        /// <summary>
        /// Builds raw block bytes by hand, so malformed blocks can be tested.
        /// </summary>
        /// <param name="count">Entry count to write.</param>
        /// <param name="entries">Alternating keys and payloads; a null payload writes the length -1.</param>
        /// <returns>Encoded block bytes.</returns>
        static byte[] BuildBlock(int count, params object[] entries) {
            using MemoryStream stream = new MemoryStream();
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian)) {
                writer.WriteInt32(count);
                for (int index = 0; index < entries.Length; index += 2) {
                    writer.WriteString((string)entries[index]);
                    writer.WriteByteArray((byte[])entries[index + 1]);
                }
            }

            return stream.ToArray();
        }
    }
}
