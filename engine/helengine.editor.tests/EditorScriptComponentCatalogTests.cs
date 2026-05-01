using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies reflection-based discovery of addable script components.
    /// </summary>
    public class EditorScriptComponentCatalogTests {
        /// <summary>
        /// Ensures the reflection helper exposes public component types with default constructors.
        /// </summary>
        [Fact]
        public void BuildDescriptors_WhenAssemblyContainsScriptComponent_ExposesDescriptorAndAddAction() {
            EditorComponentAddDescriptor descriptor = EditorScriptComponentCatalog.BuildDescriptor(typeof(TestScriptComponent));
            Assert.NotNull(descriptor);
            Assert.Equal("Test Script Component", descriptor.DisplayName);
            EditorEntity entity = new EditorEntity();

            descriptor.AddAction(entity);

            Assert.IsType<TestScriptComponent>(Assert.Single(entity.Components));
        }

        /// <summary>
        /// Dummy script component used to exercise the reflection path.
        /// </summary>
        public sealed class TestScriptComponent : Component {
            /// <summary>
            /// Initializes one test component with the default lifecycle.
            /// </summary>
            public TestScriptComponent() {
            }
        }
    }
}
