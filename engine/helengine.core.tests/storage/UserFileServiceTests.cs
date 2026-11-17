using helengine;

namespace helengine.core.tests.storage {
    /// <summary>Verifies optional user storage is exposed explicitly without assuming a filesystem root.</summary>
    public sealed class UserFileServiceTests {
        /// <summary>Unconfigured hosts report unavailable when the application requests a read.</summary>
        [Fact]
        public void MissingBackendIsExplicitlyUnavailable() {
            UserFileService service = new UserFileService(null);
            Assert.False(service.IsAvailable);
            Assert.Throws<NotSupportedException>(() => service.BeginRead("file", 100));
            Assert.Throws<NotSupportedException>(() => service.BeginList("", 10));
        }

        /// <summary>Core initialization retains the optional native service hook.</summary>
        [Fact]
        public void CoreCreatesUserFileService() {
            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test"));
            Assert.NotNull(core.UserFiles);
            Assert.False(core.UserFiles.IsAvailable);
        }

        /// <summary>Core borrows the backend and transfers the exact operation to its application owner.</summary>
        [Fact]
        public void InjectedBackendForwardsRequestsWithoutImplicitPollingOrDisposal() {
            UserFileTestBackend backend = new UserFileTestBackend();
            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory),
                UserFileBackend = backend
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test"));
            Assert.True(core.UserFiles.IsAvailable);
            IUserFileOperation operation = core.UserFiles.BeginList("", 17);
            Assert.Same(backend.Operation, operation);
            Assert.Equal("", backend.Path);
            Assert.Equal(17, backend.Limit);
            Assert.False(backend.Operation.WasAdvanced);
            Assert.Same(operation, core.UserFiles.BeginRead("café/data", 128));
            Assert.Equal("café/data", backend.Path);
            Assert.Equal(128, backend.Limit);
            Assert.False(backend.Operation.WasDisposed);
            operation.Cancel();
            operation.Advance();
            Assert.Equal(UserFileOperationStatus.Canceled, operation.Status);
            operation.Dispose();
            Assert.True(backend.Operation.WasDisposed);
        }
    }
}
