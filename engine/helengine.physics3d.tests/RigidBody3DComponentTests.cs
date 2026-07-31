namespace helengine.physics3d.tests {
    /// <summary>
    /// Verifies authored sleep settings on rigid-body components.
    /// </summary>
    public sealed class RigidBody3DComponentTests {
        /// <summary>
        /// Confirms a newly created rigid body uses the approved aggressive sleeping defaults.
        /// </summary>
        [Fact]
        public void Constructor_UsesAggressiveSleepingDefaults() {
            RigidBody3DComponent component = new RigidBody3DComponent();

            Assert.Equal(0.5d, component.SleepThreshold);
            Assert.Equal(10, component.SleepTicks);
        }

        /// <summary>
        /// Confirms zero, negative, and non-finite sleep thresholds are rejected.
        /// </summary>
        [Theory]
        [InlineData(0d)]
        [InlineData(-1d)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void SleepThreshold_WhenNotFiniteAndPositive_ThrowsArgumentOutOfRangeException(double value) {
            RigidBody3DComponent component = new RigidBody3DComponent();

            Assert.Throws<ArgumentOutOfRangeException>(() => component.SleepThreshold = value);
        }

        /// <summary>
        /// Confirms zero and negative sleep tick counts are rejected.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void SleepTicks_WhenNotPositive_ThrowsArgumentOutOfRangeException(int value) {
            RigidBody3DComponent component = new RigidBody3DComponent();

            Assert.Throws<ArgumentOutOfRangeException>(() => component.SleepTicks = value);
        }

        /// <summary>
        /// Confirms runtime ordinal restore follows declared append order rather than alphabetically reordering extensions.
        /// </summary>
        [Fact]
        public void Deserialize_WhenAppendNamesSortDifferently_PreservesDeclaredAppendOrder() {
            const string componentTypeId = "helengine.physics3d.tests.OrderedAppendRuntimeComponent";
            AutomaticScriptComponentRuntimeDeserializer deserializer = new AutomaticScriptComponentRuntimeDeserializer(
                componentTypeId,
                typeof(OrderedAppendRuntimeComponent));
            SceneComponentAssetRecord record = CreateOrderedAppendRecord(componentTypeId);

            OrderedAppendRuntimeComponent component = (OrderedAppendRuntimeComponent)deserializer.Deserialize(record, null);

            Assert.Equal(11, component.RequiredValue);
            Assert.Equal(22, component.ZuluExtension);
            Assert.Equal(33, component.AlphaExtension);
        }

        /// <summary>
        /// Creates one complete ordinal payload whose two extension values follow declared compatibility order.
        /// </summary>
        /// <param name="componentTypeId">Stable component type identifier assigned to the payload.</param>
        /// <returns>Packaged component record containing one required and two append-only integers.</returns>
        static SceneComponentAssetRecord CreateOrderedAppendRecord(string componentTypeId) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion);
            writer.WriteInt32(3);
            writer.WriteInt32(11);
            writer.WriteInt32(22);
            writer.WriteInt32(33);

            return new SceneComponentAssetRecord {
                ComponentTypeId = componentTypeId,
                ComponentIndex = 0,
                Payload = stream.ToArray()
            };
        }
    }
}
