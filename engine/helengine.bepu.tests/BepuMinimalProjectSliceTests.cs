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
            Assert.Contains("new SphereTriangleCollisionTask()", defaultTypesFileText, StringComparison.Ordinal);
            Assert.Contains("new BoxTriangleCollisionTask()", defaultTypesFileText, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the reduced default collision task registration still keeps the mesh-backed static collision handlers required by current Helengine scenes.
        /// </summary>
        [Fact]
        public void MinimalDefaultTypes_RegistersSphereAndBoxStaticMeshCollisionTasks() {
            string defaultTypesFilePath = GetMinimalDefaultTypesFilePath();
            string defaultTypesFileText = File.ReadAllText(defaultTypesFilePath);

            Assert.Contains("new ConvexCompoundCollisionTask<Sphere, Mesh", defaultTypesFileText, StringComparison.Ordinal);
            Assert.Contains("new ConvexCompoundCollisionTask<Box, Mesh", defaultTypesFileText, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the reduced BEPU physics project keeps the mesh shape and mesh collision task sources available for static-mesh runtime support.
        /// </summary>
        [Fact]
        public void MinimalBepuPhysicsProject_KeepsMeshShapeAndMeshCollisionTaskSources() {
            string projectFilePath = GetMinimalBepuPhysicsProjectFilePath();
            string projectFileText = File.ReadAllText(projectFilePath);

            Assert.DoesNotContain(@"<Compile Remove=""Collidables\Mesh.cs"" />", projectFileText, StringComparison.Ordinal);
            Assert.DoesNotContain(@"<Compile Remove=""CollisionDetection\MeshReduction.cs"" />", projectFileText, StringComparison.Ordinal);
            Assert.DoesNotContain(@"<Compile Remove=""CollisionDetection\CollisionTasks\*Mesh*.cs"" />", projectFileText, StringComparison.Ordinal);
            Assert.DoesNotContain(@"<Compile Remove=""Constraints\Contact\ContactNonconvexTypes.cs"" />", projectFileText, StringComparison.Ordinal);
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

        /// <summary>
        /// Resolves the reduced vendored BEPU physics project file path from the compiled test output location.
        /// </summary>
        /// <returns>Absolute project file path for the reduced vendored BEPU physics project.</returns>
        static string GetMinimalBepuPhysicsProjectFilePath() {
            return Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "vendor",
                "bepuphysics2",
                "BepuPhysics",
                "BepuPhysics.Minimal.csproj"));
        }
    }
}
