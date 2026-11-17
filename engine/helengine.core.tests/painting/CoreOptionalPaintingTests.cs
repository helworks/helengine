using helengine;

namespace helengine.core.tests.painting {
    /// <summary>Verifies normal Core initialization publishes optional painting and write services even on unsupported hosts.</summary>
    public sealed class CoreOptionalPaintingTests {
        /// <summary>A scene must discover explicit unavailability through Core instead of accidentally invoking a standalone painter.</summary>
        [Theory]
        [InlineData("Painting")]
        [InlineData("UserFileWrites")]
        public void UnconfiguredCorePublishesUnavailableService(string name) {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            try {
                core.Initialize(null, null, null, new PlatformInfo("test", "test"));
                System.Reflection.PropertyInfo property = typeof(Core).GetProperty(name);
                Assert.NotNull(property);
                object service = property.GetValue(core);
                Assert.NotNull(service);
                Assert.Equal(false, service.GetType().GetProperty("IsAvailable").GetValue(service));
            } finally { core.Dispose(); }
        }
    }
}
