using helengine.baseplatform.Requests;

namespace helengine.baseplatform.tests.Requests {
    /// <summary>
    /// Verifies the build execution scope carried by platform build requests.
    /// </summary>
    public sealed class PlatformBuildRequestExecutionScopeTests {
        /// <summary>The short constructor keeps full builds.</summary>
        [Fact]
        public void ShortCtor_DefaultsToFullScope() {
            PlatformBuildRequest request = PlatformBuildRequestTestFactory.Create(PlatformBuildExecutionScope.Full, useShortCtor: true);
            Assert.Equal(PlatformBuildExecutionScope.Full, request.ExecutionScope);
        }

        /// <summary>The long constructor carries an assets-only scope.</summary>
        [Fact]
        public void LongCtor_CarriesAssetsOnlyScope() {
            PlatformBuildRequest request = PlatformBuildRequestTestFactory.Create(PlatformBuildExecutionScope.AssetsOnly, useShortCtor: false);
            Assert.Equal(PlatformBuildExecutionScope.AssetsOnly, request.ExecutionScope);
        }
    }
}
