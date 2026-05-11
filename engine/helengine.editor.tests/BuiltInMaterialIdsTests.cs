using helengine;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the built-in material identifiers that drive renderer-side transform payload selection.
    /// </summary>
    public class BuiltInMaterialIdsTests {
        /// <summary>
        /// Ensures the runtime standard material id is recognized as the standard mesh transform path and that other ids are not.
        /// </summary>
        [Fact]
        public void UsesStandardMeshTransform_WhenEvaluatingMaterialIds_ReturnsTrueOnlyForTheBuiltInStandardMaterial() {
            Assert.True(BuiltInMaterialIds.UsesStandardMeshTransform(BuiltInMaterialIds.StandardRuntimeMaterialAssetId));
            Assert.False(BuiltInMaterialIds.UsesStandardMeshTransform(BuiltInMaterialIds.StandardMaterialShaderAssetId));
            Assert.False(BuiltInMaterialIds.UsesStandardMeshTransform("engine:material:gizmo"));
        }

        /// <summary>
        /// Ensures standard-forward shader selections still receive the standard mesh transform payload even when the runtime material id differs from the generated editor material id.
        /// </summary>
        [Fact]
        public void UsesStandardMeshTransform_WhenEvaluatingStandardForwardShaderSelection_ReturnsTrueForPackagedAndCustomMaterialIds() {
            Assert.True(BuiltInMaterialIds.UsesStandardMeshTransform(
                BuiltInMaterialIds.StandardMaterialShaderAssetId,
                BuiltInMaterialIds.StandardForwardShaderAssetId,
                BuiltInMaterialIds.StandardForwardVertexProgramName,
                BuiltInMaterialIds.StandardForwardPixelProgramName));
            Assert.True(BuiltInMaterialIds.UsesStandardMeshTransform(
                "project.materials.custom.standard",
                BuiltInMaterialIds.StandardForwardShaderAssetId,
                BuiltInMaterialIds.StandardForwardVertexProgramName,
                BuiltInMaterialIds.StandardForwardPixelProgramName));
            Assert.False(BuiltInMaterialIds.UsesStandardMeshTransform(
                "project.materials.custom.standard",
                BuiltInMaterialIds.StandardForwardShaderAssetId,
                "ForwardStandardShader.vs",
                "ForwardUnlitShader.ps"));
        }
    }
}
