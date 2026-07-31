using System.Security.Cryptography;
using helengine.editor.tests.testing;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the isolated HelPhysics proof command removes only menu-return behavior from a copied Demodisc stack scene.
/// </summary>
public sealed class CityHelPhysicsStackTestScenePreparationTests : IDisposable {
    /// <summary>
    /// Environment variable that supplies the Demodisc project root containing the authored source scene and proof command.
    /// </summary>
    const string DemodiscProjectRootEnvironmentVariableName = "HELENGINE_DEMODISC_HELPHYSICS_STACK_TEST_ROOT";

    /// <summary>
    /// Relative path to the real authored stacked-box scene beneath a project root.
    /// </summary>
    const string StackSceneRelativePath = @"assets\scenes\physics\test_scene_dynamic_stack_boxes.helen";

    /// <summary>
    /// Stable command identifier executed by the release build prebuild phase.
    /// </summary>
    const string PreparationCommandId = "physics.prepare-helphysics-stack-test-scene";

    /// <summary>
    /// Stable automatic persistence id of the runtime menu-return component removed from the copied scene.
    /// </summary>
    const string ReturnToMenuComponentTypeId = "city.menu.DemoDiscReturnToMenuComponent, gameplay";

    /// <summary>
    /// Stable built-in rigid-body persistence id used to inspect retained physics bodies.
    /// </summary>
    const string RigidBodyComponentTypeId = "helengine.RigidBody3DComponent";

    /// <summary>
    /// Stable built-in box-collider persistence id used to verify every retained body keeps its collider.
    /// </summary>
    const string BoxColliderComponentTypeId = "helengine.BoxCollider3DComponent";

    /// <summary>
    /// Visible workspace-owned root under which the focused test may create and remove one copied project.
    /// </summary>
    const string TestOutputRootPath = @"C:\dev\helworks\builds\helphysics-demodisc-stack-test\tests";

    /// <summary>
    /// Unique copied project root used by this test instance.
    /// </summary>
    readonly string CopiedProjectRootPath;

    /// <summary>
    /// Absolute caller-supplied Demodisc project root used as the immutable source for this integration test.
    /// </summary>
    readonly string DemodiscProjectRootPath;

    /// <summary>
    /// Creates one unique visible copied-project root without touching the authored Demodisc worktree scene.
    /// </summary>
    public CityHelPhysicsStackTestScenePreparationTests() {
        string configuredDemodiscProjectRootPath = Environment.GetEnvironmentVariable(DemodiscProjectRootEnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(configuredDemodiscProjectRootPath)) {
            throw new InvalidOperationException($"Set '{DemodiscProjectRootEnvironmentVariableName}' to the Demodisc project root before running this integration test.");
        }

        DemodiscProjectRootPath = Path.GetFullPath(configuredDemodiscProjectRootPath);
        if (!Directory.Exists(DemodiscProjectRootPath)) {
            throw new DirectoryNotFoundException($"The configured Demodisc project root '{DemodiscProjectRootPath}' does not exist.");
        }

        CopiedProjectRootPath = Path.Combine(
            TestOutputRootPath,
            "scene-preparation",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(CopiedProjectRootPath, "assets", "scenes", "physics"));
    }

    /// <summary>
    /// Removes only the validated workspace-owned copied-project root created for this test.
    /// </summary>
    public void Dispose() {
        string fullTestOutputRootPath = Path.GetFullPath(TestOutputRootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullCopiedProjectRootPath = Path.GetFullPath(CopiedProjectRootPath);
        if (!fullCopiedProjectRootPath.StartsWith(fullTestOutputRootPath, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException($"Refusing to remove test output outside '{fullTestOutputRootPath}'.");
        }
        if (Directory.Exists(fullCopiedProjectRootPath)) {
            Directory.Delete(fullCopiedProjectRootPath, true);
        }
    }

    /// <summary>
    /// Copies the committed scene, executes the real project command, and proves source immutability plus retained Windows box-body structure.
    /// </summary>
    [Fact]
    public void Preparation_command_removes_only_return_to_menu_from_copied_stack_scene() {
        string sourceScenePath = Path.Combine(DemodiscProjectRootPath, StackSceneRelativePath);
        string copiedScenePath = Path.Combine(CopiedProjectRootPath, StackSceneRelativePath);
        byte[] sourceHashBefore = SHA256.HashData(File.ReadAllBytes(sourceScenePath));
        byte[] expectedPreparedSceneBytes = BuildExpectedPreparedSceneBytes(sourceScenePath);
        CopyProjectCommandSources();
        File.Copy(sourceScenePath, copiedScenePath, true);

        using EditorGameScriptHotReloadService hotReloadService = CreateHotReloadService(CopiedProjectRootPath);
        EditorBuildExecutionResult buildResult = hotReloadService.BuildAndReload();
        Assert.True(buildResult.Succeeded, buildResult.Message);
        EditorCommandExecutionService executionService = new EditorCommandExecutionService(
            hotReloadService,
            new EditorCommandContext(CopiedProjectRootPath, hotReloadService.ScriptTypeResolver));

        executionService.Execute(PreparationCommandId);

        Assert.Equal(sourceHashBefore, SHA256.HashData(File.ReadAllBytes(sourceScenePath)));
        Assert.Equal(expectedPreparedSceneBytes, File.ReadAllBytes(copiedScenePath));
        SceneAsset preparedScene = LoadScene(copiedScenePath);
        Assert.DoesNotContain(
            EnumerateComponentRecords(preparedScene.RootEntities),
            record => string.Equals(record.ComponentTypeId, ReturnToMenuComponentTypeId, StringComparison.Ordinal));

        List<BodyKind3D> retainedBoxBodyKinds = CollectWindowsBoxBodyKinds(preparedScene.RootEntities);
        Assert.Equal(5, retainedBoxBodyKinds.Count);
        Assert.Equal(1, retainedBoxBodyKinds.Count(bodyKind => bodyKind == BodyKind3D.Static));
        Assert.Equal(4, retainedBoxBodyKinds.Count(bodyKind => bodyKind == BodyKind3D.Dynamic));
    }

    /// <summary>
    /// Copies the isolated Demodisc project file and code modules into the disposable project used for command execution.
    /// </summary>
    void CopyProjectCommandSources() {
        File.Copy(
            Path.Combine(DemodiscProjectRootPath, "project.heproj"),
            Path.Combine(CopiedProjectRootPath, "project.heproj"),
            true);
        CopyDirectory(
            Path.Combine(DemodiscProjectRootPath, "assets", "codebase"),
            Path.Combine(CopiedProjectRootPath, "assets", "codebase"));
    }

    /// <summary>
    /// Copies one complete directory tree into the disposable project without modifying the source worktree.
    /// </summary>
    /// <param name="sourceDirectoryPath">Existing source directory to copy.</param>
    /// <param name="destinationDirectoryPath">Destination directory created beneath the validated test root.</param>
    static void CopyDirectory(string sourceDirectoryPath, string destinationDirectoryPath) {
        Directory.CreateDirectory(destinationDirectoryPath);
        string[] files = Directory.GetFiles(sourceDirectoryPath);
        for (int fileIndex = 0; fileIndex < files.Length; fileIndex++) {
            File.Copy(files[fileIndex], Path.Combine(destinationDirectoryPath, Path.GetFileName(files[fileIndex])), true);
        }

        string[] directories = Directory.GetDirectories(sourceDirectoryPath);
        for (int directoryIndex = 0; directoryIndex < directories.Length; directoryIndex++) {
            CopyDirectory(
                directories[directoryIndex],
                Path.Combine(destinationDirectoryPath, Path.GetFileName(directories[directoryIndex])));
        }
    }

    /// <summary>
    /// Creates the real generated-module build and collectible assembly host rooted at the disposable copied project.
    /// </summary>
    /// <param name="projectRootPath">Disposable project root containing copied command sources.</param>
    /// <returns>Hot-reload service that discovers the project-authored preparation command.</returns>
    static EditorGameScriptHotReloadService CreateHotReloadService(string projectRootPath) {
        EditorGameSolutionService solutionService = new EditorGameSolutionService(
            projectRootPath,
            "city",
            new TestEditorIdeLauncher());
        return new EditorGameScriptHotReloadService(
            solutionService,
            new EditorDotNetScriptBuildTool(),
            new EditorGameScriptAssemblyHost(projectRootPath));
    }

    /// <summary>
    /// Loads one authored scene through the editor asset serializer.
    /// </summary>
    /// <param name="scenePath">Absolute scene path to deserialize.</param>
    /// <returns>Deserialized scene asset.</returns>
    static SceneAsset LoadScene(string scenePath) {
        using FileStream stream = File.OpenRead(scenePath);
        return Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
    }

    /// <summary>
    /// Builds the exact expected serialized scene by removing only the one return component from an in-memory source snapshot.
    /// </summary>
    /// <param name="sourceScenePath">Absolute authored source scene path.</param>
    /// <returns>Canonical serialized bytes expected after the real command executes.</returns>
    static byte[] BuildExpectedPreparedSceneBytes(string sourceScenePath) {
        SceneAsset expectedScene = LoadScene(sourceScenePath);
        int removedComponentCount = RemoveExpectedReturnComponents(expectedScene.RootEntities ?? Array.Empty<SceneEntityAsset>());
        Assert.Equal(1, removedComponentCount);
        return AssetSerializer.SerializeToBytes(expectedScene);
    }

    /// <summary>
    /// Removes only the expected return records from one in-memory test oracle while preserving every other serialized object.
    /// </summary>
    /// <param name="entities">Serialized hierarchy used to form the exact expected output.</param>
    /// <returns>Number of matching return records removed.</returns>
    static int RemoveExpectedReturnComponents(IReadOnlyList<SceneEntityAsset> entities) {
        int removedComponentCount = 0;
        for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
            SceneEntityAsset entity = entities[entityIndex];
            SceneComponentAssetRecord[] components = entity.Components ?? Array.Empty<SceneComponentAssetRecord>();
            List<SceneComponentAssetRecord> retainedComponents = new List<SceneComponentAssetRecord>(components.Length);
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++) {
                SceneComponentAssetRecord component = components[componentIndex];
                if (string.Equals(component.ComponentTypeId, ReturnToMenuComponentTypeId, StringComparison.Ordinal)) {
                    removedComponentCount++;
                } else {
                    retainedComponents.Add(component);
                }
            }

            entity.Components = retainedComponents.ToArray();
            removedComponentCount += RemoveExpectedReturnComponents(entity.Children ?? Array.Empty<SceneEntityAsset>());
        }

        return removedComponentCount;
    }

    /// <summary>
    /// Enumerates every serialized component record in one complete scene hierarchy.
    /// </summary>
    /// <param name="entities">Serialized roots to traverse.</param>
    /// <returns>All component records in deterministic hierarchy order.</returns>
    static IEnumerable<SceneComponentAssetRecord> EnumerateComponentRecords(IReadOnlyList<SceneEntityAsset> entities) {
        if (entities == null) {
            throw new ArgumentNullException(nameof(entities));
        }

        for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
            SceneEntityAsset entity = entities[entityIndex];
            SceneComponentAssetRecord[] components = entity.Components ?? Array.Empty<SceneComponentAssetRecord>();
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++) {
                yield return components[componentIndex];
            }

            foreach (SceneComponentAssetRecord descendantRecord in EnumerateComponentRecords(entity.Children ?? Array.Empty<SceneEntityAsset>())) {
                yield return descendantRecord;
            }
        }
    }

    /// <summary>
    /// Collects body kinds for Windows-visible entities that retain both one rigid body and one box collider.
    /// </summary>
    /// <param name="entities">Serialized entities to inspect recursively.</param>
    /// <returns>Body kinds for every paired Windows box body.</returns>
    static List<BodyKind3D> CollectWindowsBoxBodyKinds(IReadOnlyList<SceneEntityAsset> entities) {
        if (entities == null) {
            throw new ArgumentNullException(nameof(entities));
        }

        List<BodyKind3D> bodyKinds = new List<BodyKind3D>();
        for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
            SceneEntityAsset entity = entities[entityIndex];
            if (!ExistsOnWindows(entity)) {
                continue;
            }

            SceneComponentAssetRecord rigidBodyRecord = FindComponent(entity, RigidBodyComponentTypeId);
            SceneComponentAssetRecord boxColliderRecord = FindComponent(entity, BoxColliderComponentTypeId);
            if (rigidBodyRecord != null && boxColliderRecord != null) {
                bodyKinds.Add(ReadBodyKind(rigidBodyRecord));
            }

            bodyKinds.AddRange(CollectWindowsBoxBodyKinds(entity.Children ?? Array.Empty<SceneEntityAsset>()));
        }

        return bodyKinds;
    }

    /// <summary>
    /// Resolves one exact component record from a serialized entity when present.
    /// </summary>
    /// <param name="entity">Serialized entity whose components should be searched.</param>
    /// <param name="componentTypeId">Stable component type id to match.</param>
    /// <returns>Matching component record, or null when absent.</returns>
    static SceneComponentAssetRecord FindComponent(SceneEntityAsset entity, string componentTypeId) {
        SceneComponentAssetRecord[] components = entity.Components ?? Array.Empty<SceneComponentAssetRecord>();
        for (int index = 0; index < components.Length; index++) {
            if (string.Equals(components[index].ComponentTypeId, componentTypeId, StringComparison.Ordinal)) {
                return components[index];
            }
        }

        return null;
    }

    /// <summary>
    /// Determines whether one authored entity remains present when the Windows platform is packaged.
    /// </summary>
    /// <param name="entity">Serialized entity whose platform existence overrides should be evaluated.</param>
    /// <returns>True when no Windows exclusion exists or Windows is explicitly enabled.</returns>
    static bool ExistsOnWindows(SceneEntityAsset entity) {
        SceneEntityPlatformExistenceOverrideAsset[] overrides = entity.PlatformExistenceOverrides ?? Array.Empty<SceneEntityPlatformExistenceOverrideAsset>();
        for (int index = 0; index < overrides.Length; index++) {
            SceneEntityPlatformExistenceOverrideAsset existenceOverride = overrides[index];
            if (existenceOverride != null && string.Equals(existenceOverride.PlatformId, "windows", StringComparison.OrdinalIgnoreCase)) {
                return existenceOverride.Exists;
            }
        }

        return true;
    }

    /// <summary>
    /// Reads the tagged BodyKind field from one current authored rigid-body component record.
    /// </summary>
    /// <param name="record">Rigid-body component record to decode.</param>
    /// <returns>Serialized rigid-body participation kind.</returns>
    static BodyKind3D ReadBodyKind(SceneComponentAssetRecord record) {
        EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(record.Payload);
        if (!reader.TryGetFieldReader("BodyKind", out EngineBinaryReader bodyKindReader)) {
            throw new InvalidOperationException("Rigid-body record did not contain its required BodyKind field.");
        }

        using (bodyKindReader) {
            return (BodyKind3D)bodyKindReader.ReadInt32();
        }
    }
}
