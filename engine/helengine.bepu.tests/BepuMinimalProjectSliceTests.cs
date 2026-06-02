namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies the Helengine BEPU integration is wired to the reduced vendored project slice instead of the full upstream project graph.
    /// </summary>
    public sealed class BepuMinimalProjectSliceTests {
        /// <summary>
        /// Ensures the Helengine BEPU project references the reduced BEPU physics project.
        /// </summary>
        [Fact]
        public void HelengineBepuProject_ReferencesMinimalBepuPhysicsProject() {
            string projectFilePath = GetHelengineBepuProjectFilePath();
            string projectFileText = File.ReadAllText(projectFilePath);

            Assert.Contains(@"..\vendor\bepuphysics2\BepuPhysics\BepuPhysics.Minimal.csproj", projectFileText, StringComparison.Ordinal);
            Assert.DoesNotContain(@"..\vendor\bepuphysics2\BepuPhysics\BepuPhysics.csproj", projectFileText, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the Helengine BEPU project references the reduced BEPU utilities project.
        /// </summary>
        [Fact]
        public void HelengineBepuProject_ReferencesMinimalBepuUtilitiesProject() {
            string projectFilePath = GetHelengineBepuProjectFilePath();
            string projectFileText = File.ReadAllText(projectFilePath);

            Assert.Contains(@"..\vendor\bepuphysics2\BepuUtilities\BepuUtilities.Minimal.csproj", projectFileText, StringComparison.Ordinal);
            Assert.DoesNotContain(@"..\vendor\bepuphysics2\BepuUtilities\BepuUtilities.csproj", projectFileText, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the reduced default collision task registration uses concrete task types so generated native dispatch does not depend on open generic task casts.
        /// </summary>
        [Fact]
        public void MinimalDefaultTypes_RegistersConcreteCollisionTaskTypes() {
            string defaultTypesFilePath = GetMinimalDefaultTypesFilePath();
            string defaultTypesFileText = File.ReadAllText(defaultTypesFilePath);

            Assert.DoesNotContain("new ConvexCollisionTask<", defaultTypesFileText, StringComparison.Ordinal);
            Assert.Contains("new SphereSphereCollisionTask()", defaultTypesFileText, StringComparison.Ordinal);
            Assert.Contains("new SphereBoxCollisionTask()", defaultTypesFileText, StringComparison.Ordinal);
            Assert.Contains("new BoxBoxCollisionTask()", defaultTypesFileText, StringComparison.Ordinal);
        }

        /// <summary>
        /// Resolves the Helengine BEPU project file path from the compiled test output location.
        /// </summary>
        /// <returns>Absolute project file path for the live Helengine BEPU project.</returns>
        static string GetHelengineBepuProjectFilePath() {
            return Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "helengine.bepu",
                "helengine.bepu.csproj"));
        }

        /// <summary>
        /// Resolves the reduced vendored default types source file path from the compiled test output location.
        /// </summary>
        /// <returns>Absolute source file path for the reduced vendored default task registrations.</returns>
        static string GetMinimalDefaultTypesFilePath() {
            return Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "vendor",
                "bepuphysics2",
                "BepuPhysics",
                "Minimal",
                "DefaultTypes.Minimal.cs"));
        }
    }
}
