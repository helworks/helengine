using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor-owned project paths keep shader cache output under the shared project cache root.
    /// </summary>
    public sealed class EditorProjectPathsTests {
        /// <summary>
        /// Ensures initializing editor project paths places the shader cache under the project cache directory.
        /// </summary>
        [Fact]
        public void Initialize_WhenProjectRootIsProvided_PlacesShaderCacheUnderCacheDirectory() {
            string projectRootPath = Path.Combine(Path.GetTempPath(), "helengine-project-path-tests", Guid.NewGuid().ToString("N"));

            EditorProjectPaths.Initialize(projectRootPath);

            Assert.Equal(Path.Combine(Path.GetFullPath(projectRootPath), "cache", "shader-cache"), EditorProjectPaths.ShaderCache);
        }

        /// <summary>
        /// Ensures the editor session resolves shader package output under the shared project cache directory.
        /// </summary>
        [Fact]
        public void ResolveShaderPackageOutputPath_WhenProjectRootIsProvided_PlacesShaderCacheUnderCacheDirectory() {
            string projectRootPath = Path.Combine(Path.GetTempPath(), "helengine-project-path-tests", Guid.NewGuid().ToString("N"));
            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            MethodInfo method = typeof(EditorSession).GetMethod("ResolveShaderPackageOutputPath", BindingFlags.Instance | BindingFlags.NonPublic);

            string shaderCachePath = (string)method.Invoke(session, new object[] { projectRootPath });

            Assert.Equal(Path.Combine(Path.GetFullPath(projectRootPath), "cache", "shader-cache"), shaderCachePath);
        }
    }
}
