namespace helengine.core.tests.storage {
    /// <summary>Verifies that optional write support does not turn existing read-only hosts into writers.</summary>
    public sealed class UserFileWriteServiceTests {
        /// <summary>Unsupported hosts must explicitly reject either write operation before any backend work occurs.</summary>
        [Fact]
        public void MissingWriteBackendIsExplicitlyUnavailable() {
            UserFileWriteService service = new UserFileWriteService(null);
            Assert.False(service.IsAvailable);
            Assert.Throws<NotSupportedException>(() => service.BeginPublish("asset", new byte[] { 1 }, new string('0', 64), 1L, 1));
            Assert.Throws<NotSupportedException>(() => service.BeginReplace("descriptor", new byte[] { 1 }, new string('0', 64), Array.Empty<UserFilePrecondition>(), 1));
        }
    }
}
