using System.Reflection;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that component property edit requests are validated and applied through the inspector mutation boundary.
    /// </summary>
    public sealed class ComponentPropertyEditControllerTests {
        /// <summary>
        /// Confirms that a common-scope scalar edit changes the requested component and emits one scene mutation notification.
        /// </summary>
        [Fact]
        public void Apply_WhenRequestTargetsWritableProperty_ChangesValueAndMarksSceneDirty() {
            TestInspectorComponent component = new TestInspectorComponent();
            int sceneMutationCount = 0;
            ComponentPropertyEditController controller = new ComponentPropertyEditController(
                new ComponentPlatformEditingService(),
                () => null,
                () => sceneMutationCount++,
                (_) => { });
            PropertyInfo property = typeof(TestInspectorComponent).GetProperty(nameof(TestInspectorComponent.Value));
            ComponentPropertyEditRequest request = new ComponentPropertyEditRequest(
                null,
                component,
                component,
                null,
                property,
                nameof(TestInspectorComponent.Value),
                4,
                new EditorOverrideScope(ComponentPlatformEditingService.CommonPlatformId),
                nameof(TestInspectorComponent.Value),
                false);

            controller.Apply(request);

            Assert.Equal(4, component.Value);
            Assert.Equal(1, sceneMutationCount);
        }

        /// <summary>
        /// Confirms that assigning the existing value does not create a dirty mutation or presentation refresh.
        /// </summary>
        [Fact]
        public void Apply_WhenValueIsUnchanged_DoesNotRecordMutation() {
            TestInspectorComponent component = new TestInspectorComponent();
            int sceneMutationCount = 0;
            int refreshCount = 0;
            ComponentPropertyEditController controller = new ComponentPropertyEditController(
                new ComponentPlatformEditingService(),
                () => null,
                () => sceneMutationCount++,
                (_) => refreshCount++);
            PropertyInfo property = typeof(TestInspectorComponent).GetProperty(nameof(TestInspectorComponent.Value));
            ComponentPropertyEditRequest request = new ComponentPropertyEditRequest(
                null,
                component,
                component,
                null,
                property,
                nameof(TestInspectorComponent.Value),
                component.Value,
                new EditorOverrideScope(ComponentPlatformEditingService.CommonPlatformId),
                nameof(TestInspectorComponent.Value),
                false);

            controller.Apply(request);

            Assert.Equal(0, sceneMutationCount);
            Assert.Equal(0, refreshCount);
        }

        /// <summary>
        /// Confirms that read-only requests are rejected before changing the component or emitting a mutation.
        /// </summary>
        [Fact]
        public void Apply_WhenRequestIsReadOnly_RejectsMutation() {
            TestInspectorComponent component = new TestInspectorComponent();
            int sceneMutationCount = 0;
            ComponentPropertyEditController controller = new ComponentPropertyEditController(
                new ComponentPlatformEditingService(),
                () => null,
                () => sceneMutationCount++,
                (_) => { });
            PropertyInfo property = typeof(TestInspectorComponent).GetProperty(nameof(TestInspectorComponent.Value));
            ComponentPropertyEditRequest request = new ComponentPropertyEditRequest(
                null,
                component,
                component,
                null,
                property,
                nameof(TestInspectorComponent.Value),
                4,
                new EditorOverrideScope(ComponentPlatformEditingService.CommonPlatformId),
                nameof(TestInspectorComponent.Value),
                true);

            Assert.Throws<InvalidOperationException>(() => controller.Apply(request));
            Assert.Equal(1, component.Value);
            Assert.Equal(0, sceneMutationCount);
        }
    }
}
