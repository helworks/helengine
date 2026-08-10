namespace helengine.editor.tests {
    public sealed class BuildDialogAddRequestTests {
        [Fact]
        public void Constructor_WhenEnvironmentIsProvided_PreservesEnvironmentId() {
            BuildDialogAddRequest request = new BuildDialogAddRequest(
                "windows",
                ["Scenes/Main.helen"],
                @"C:\\builds\\windows",
                false,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                "qa");

            Assert.Equal("qa", request.SelectedEnvironmentId);
        }
    }
}
