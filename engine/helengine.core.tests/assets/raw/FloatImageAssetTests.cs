using helengine;
using Xunit;

namespace helengine.core.tests.assets.raw {
    public class FloatImageAssetTests {
        [Fact]
        public void Dispose_ReleasesPixelBuffer() {
            var asset = new FloatImageAsset {
                Id = "test-image",
                Width = 2,
                Height = 2,
                Pixels = new float[2 * 2 * 4]
            };

            asset.Dispose();

            Assert.Null(asset.Pixels);
        }
    }
}
