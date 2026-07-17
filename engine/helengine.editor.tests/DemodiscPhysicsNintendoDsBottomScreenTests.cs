namespace helengine.editor.tests;

/// <summary>
/// Verifies that generated demodisc physics Nintendo DS scenes place their authored 2D UI on the shared bottom screen.
/// </summary>
public sealed class DemodiscPhysicsNintendoDsBottomScreenTests {
    /// <summary>
    /// Stable matrix physics scene asset used to verify the generated dual-screen hierarchy.
    /// </summary>
    const string MatrixScenePath = @"C:\dev\helprojs\demodisc\assets\scenes\physics\test_scene_matrix_render.helen";

    /// <summary>
    /// Curated handheld physics scenes that must all carry the shared Nintendo handheld bottom-screen scaffold.
    /// </summary>
    static readonly string[] PhysicsScenePaths = {
        @"C:\dev\helprojs\demodisc\assets\scenes\physics\test_scene_dynamic_mixed_stack.helen",
        @"C:\dev\helprojs\demodisc\assets\scenes\physics\test_scene_dynamic_sphere_stack.helen",
        @"C:\dev\helprojs\demodisc\assets\scenes\physics\test_scene_dynamic_stack_boxes.helen",
        MatrixScenePath,
        @"C:\dev\helprojs\demodisc\assets\scenes\physics\test_scene_static_mesh_minimal.helen",
        @"C:\dev\helprojs\demodisc\assets\scenes\physics\test_scene_static_mesh_showcase.helen"
    };

    /// <summary>
    /// Ensures the matrix phase-status overlay is serialized below the shared bottom-screen viewport.
    /// </summary>
    [Fact]
    public void Matrix_render_phase_status_is_serialized_under_the_shared_bottom_screen_root() {
        SceneAsset scene = LoadMatrixScene();
        SceneEntityAsset bottomScreenCamera = FindRequiredEntity(scene.RootEntities, "DemoDiscBottomScreenCamera");
        SceneEntityAsset bottomScreenRoot = FindRequiredEntity(bottomScreenCamera.Children, "DemoDiscBottomScreenRoot");
        SceneEntityAsset phaseStatus = FindRequiredEntity(bottomScreenRoot.Children, "MatrixRenderPhaseStatus");

        Assert.InRange(phaseStatus.LocalPosition.Y, 0f, 191f);
    }

    /// <summary>
    /// Ensures every curated physics scene carries the same bottom-screen camera, root, light control, and back control.
    /// </summary>
    [Fact]
    public void Every_curated_physics_scene_uses_the_shared_bottom_screen_scaffold() {
        foreach (string scenePath in PhysicsScenePaths) {
            using FileStream stream = File.OpenRead(scenePath);
            SceneAsset scene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            SceneEntityAsset bottomScreenCamera = FindRequiredEntity(scene.RootEntities, "DemoDiscBottomScreenCamera");
            SceneEntityAsset bottomScreenRoot = FindRequiredEntity(bottomScreenCamera.Children, "DemoDiscBottomScreenRoot");

            FindRequiredEntity(bottomScreenRoot.Children, "DemoDiscBottomScreenLightButton");
            FindRequiredEntity(bottomScreenRoot.Children, "DemoDiscBottomScreenBackButton");
            Assert.Contains(
                EnumerateComponents(bottomScreenRoot),
                component => string.Equals(
                    component.ComponentTypeId,
                    AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(FPSComponent)),
                    StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Loads the generated matrix scene from the demodisc project.
    /// </summary>
    /// <returns>Deserialized matrix scene asset.</returns>
    static SceneAsset LoadMatrixScene() {
        using FileStream stream = File.OpenRead(MatrixScenePath);
        return Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
    }

    /// <summary>
    /// Finds one named entity recursively beneath the supplied serialized entity set.
    /// </summary>
    /// <param name="entities">Serialized entities to search.</param>
    /// <param name="name">Expected entity name.</param>
    /// <returns>Matching serialized entity.</returns>
    static SceneEntityAsset FindRequiredEntity(SceneEntityAsset[] entities, string name) {
        foreach (SceneEntityAsset entity in entities ?? Array.Empty<SceneEntityAsset>()) {
            if (string.Equals(entity.Name, name, StringComparison.Ordinal)) {
                return entity;
            }

            SceneEntityAsset descendant = FindEntity(entity.Children, name);
            if (descendant != null) {
                return descendant;
            }
        }

        throw new Xunit.Sdk.XunitException($"Could not find serialized entity '{name}'.");
    }

    /// <summary>
    /// Finds one named entity recursively beneath the supplied serialized entity set when present.
    /// </summary>
    /// <param name="entities">Serialized entities to search.</param>
    /// <param name="name">Expected entity name.</param>
    /// <returns>Matching serialized entity, or null when no match exists.</returns>
    static SceneEntityAsset FindEntity(SceneEntityAsset[] entities, string name) {
        foreach (SceneEntityAsset entity in entities ?? Array.Empty<SceneEntityAsset>()) {
            if (string.Equals(entity.Name, name, StringComparison.Ordinal)) {
                return entity;
            }

            SceneEntityAsset descendant = FindEntity(entity.Children, name);
            if (descendant != null) {
                return descendant;
            }
        }

        return null;
    }

    /// <summary>
    /// Enumerates serialized component records in one entity subtree.
    /// </summary>
    /// <param name="entity">Serialized subtree to inspect.</param>
    /// <returns>Component records owned by the subtree.</returns>
    static IEnumerable<SceneComponentAssetRecord> EnumerateComponents(SceneEntityAsset entity) {
        foreach (SceneComponentAssetRecord component in entity.Components ?? Array.Empty<SceneComponentAssetRecord>()) {
            yield return component;
        }

        foreach (SceneEntityAsset child in entity.Children ?? Array.Empty<SceneEntityAsset>()) {
            foreach (SceneComponentAssetRecord component in EnumerateComponents(child)) {
                yield return component;
            }
        }
    }
}
