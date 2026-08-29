using System.Reflection;
using helengine.directx11;
using helengine.editor.tests.testing;
using helengine.projectfile;
using helengine.platforms;
using helengine.ui;
using helengine.vulkan;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies that generated asset graphs are isolated by owner and remain usable
/// when another graph is disposed.
/// </summary>
public sealed class GeneratedSessionIsolationBehaviorTests {
    [Fact]
    public void TestGeneratedAssetGraph_DisposeClearsCoreSlotBeforeDisposingOwnedInteractionGraph() {
        Core core = CreateCore();
        TestGeneratedAssetGraph graph = new TestGeneratedAssetGraph(core);
        EditorSessionInteractionServices interactions = graph.InteractionServices;

        graph.Dispose();

        Assert.Null(core.SessionInteractionGraph);
        Assert.Throws<ObjectDisposedException>(() => interactions.InputCapture.IsPointerBlocked(new int2(0, 0)));
        core.Dispose();
    }

    [Fact]
    public void ActualEditorSessions_KeepRendererGraphsIndependentAfterSessionADisposes() {
        string projectRootA = CreateProjectRoot();
        string projectRootB = CreateProjectRoot();
        EditorSession sessionA = null;
        EditorSession sessionB = null;

        try {
            sessionA = CreateActualEditorSession(projectRootA);
            sessionB = CreateActualEditorSession(projectRootB);

            // Materialize a secondary panel for A after B owns the ambient
            // legacy core. The workspace composition must adopt the complete
            // panel subtree into A before it can register or interact.
            sessionA.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowProperties);
            EditorWorkspacePanelInstance secondaryPropertiesPanel = Assert.Single(
                sessionA.GetPanelInstancesForTest("properties")
                    .Where(instance => !string.Equals(instance.InstanceId, "properties-primary", StringComparison.OrdinalIgnoreCase)));
            Assert.Same(sessionA.Core, secondaryPropertiesPanel.Dockable.OwnerCore);
            Assert.Same(sessionA.InteractionServices, secondaryPropertiesPanel.Dockable.InteractionServices);

            EditorSessionRendererResources rendererResourcesA = GetPrivateField<EditorSessionRendererResources>(sessionA, "rendererResources");
            EditorSessionRendererResources rendererResourcesB = GetPrivateField<EditorSessionRendererResources>(sessionB, "rendererResources");
            GeneratedAssetProviderRegistry registryA = GetPrivateField<GeneratedAssetProviderRegistry>(sessionA, "generatedAssetProviderRegistry");
            GeneratedAssetProviderRegistry registryB = GetPrivateField<GeneratedAssetProviderRegistry>(sessionB, "generatedAssetProviderRegistry");
            EngineGeneratedModelCache modelCacheA = GetPrivateField<EngineGeneratedModelCache>(sessionA, "generatedModelCache");
            EngineGeneratedModelCache modelCacheB = GetPrivateField<EngineGeneratedModelCache>(sessionB, "generatedModelCache");

            Assert.NotSame(rendererResourcesA, rendererResourcesB);
            Assert.NotSame(rendererResourcesA.RenderManager3D, rendererResourcesB.RenderManager3D);
            Assert.NotSame(sessionA.Core.RenderManager2D.PixelTexture, sessionB.Core.RenderManager2D.PixelTexture);
            Assert.NotSame(registryA, registryB);
            Assert.NotSame(modelCacheA, modelCacheB);

            sessionB.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowProperties);
            EditorWorkspacePanelInstance secondaryPropertiesPanelB = Assert.Single(
                sessionB.GetPanelInstancesForTest("properties")
                    .Where(instance => !string.Equals(instance.InstanceId, "properties-primary", StringComparison.OrdinalIgnoreCase)));
            Assert.Same(sessionB.Core, secondaryPropertiesPanelB.Dockable.OwnerCore);
            Assert.Same(sessionB.InteractionServices, secondaryPropertiesPanelB.Dockable.InteractionServices);
            int focusTargetCountB;

            EditorEntity selectedEntityB = new EditorEntity(sessionB.Core, sessionB.InteractionServices) {
                Name = "Session B entity"
            };
            CameraComponent cameraB = new CameraComponent();
            selectedEntityB.AddComponent(cameraB);
            sessionB.InteractionServices.Selection.SetSelectedEntity(selectedEntityB);
            sessionB.InteractionServices.InputCapture.SetBlocker(selectedEntityB, new int2(2, 3), new int2(20, 20));
            sessionB.InteractionServices.ViewportTool.SetToolMode(cameraB, EditorViewportToolMode.Translate);
            sessionB.InteractionServices.GizmoHover.SetHoveredHandle(cameraB, selectedEntityB);
            sessionB.InteractionServices.GizmoDrag.BeginDrag(cameraB, selectedEntityB);
            focusTargetCountB = GetFocusTargetCount(sessionB.InteractionServices);
            Assert.True(focusTargetCountB > 0);

            AssetBrowserEntry cubeEntry = AssetBrowserEntry.CreateGeneratedAsset(
                "Cube",
                EngineGeneratedAssetProvider.CubeRelativePath,
                AssetEntryKind.Model,
                EngineGeneratedAssetProvider.ProviderIdValue,
                EngineGeneratedModelCache.CubeAssetId);
            RuntimeModel modelA = registryA.ResolveRuntimeModel(cubeEntry);
            RuntimeModel modelB = registryB.ResolveRuntimeModel(cubeEntry);
            Assert.NotSame(modelA, modelB);

            SceneSaveService saveServiceB = GetPrivateField<SceneSaveService>(sessionB, "SceneSaveService");
            string savedScenePath = Path.Combine(projectRootB, "assets", "Scenes", "Isolation.helen");
            saveServiceB.Save(savedScenePath);

            sessionA.Dispose();
            sessionA = null;

            string savedScenePathAfterPeerDispose = Path.Combine(projectRootB, "assets", "Scenes", "IsolationAfterPeerDispose.helen");
            saveServiceB.Save(savedScenePathAfterPeerDispose);
            Assert.Same(modelB, registryB.ResolveRuntimeModel(cubeEntry));
            Assert.False(sessionB.Core.RenderManager2D.PixelTexture.IsDisposed);
            Assert.Same(selectedEntityB, sessionB.InteractionServices.Selection.SelectedEntity);
            Assert.Equal(focusTargetCountB, GetFocusTargetCount(sessionB.InteractionServices));
            Assert.True(sessionB.InteractionServices.InputCapture.IsPointerBlocked(new int2(4, 4)));
            Assert.Same(selectedEntityB, sessionB.InteractionServices.GizmoHover.GetHoveredHandle(cameraB));
            Assert.True(sessionB.InteractionServices.GizmoDrag.IsDragging(cameraB));
            sessionB.Core.ObjectManager.Update();
            sessionB.Core.Update();
            sessionB.Core.Draw();
            Assert.True(Assert.IsType<TestDirectX11RenderManager3D>(sessionB.Core.RenderManager3D).DrawCallCount > 0);
            Assert.Same(sessionB.Core, secondaryPropertiesPanelB.Dockable.OwnerCore);
            Assert.True(File.Exists(savedScenePath));
            Assert.True(File.Exists(savedScenePathAfterPeerDispose));

            sessionB.InteractionServices.GizmoDrag.EndDrag(cameraB);
            selectedEntityB.Dispose();
            Assert.DoesNotContain(selectedEntityB, sessionB.Core.ObjectManager.Entities);
        } finally {
            sessionA?.Dispose();
            sessionB?.Dispose();
            DeleteProjectRoot(projectRootA);
            DeleteProjectRoot(projectRootB);
        }
    }

    [Fact]
    public void ActualEditorSessions_KeepSessionAUsableAfterSessionBDisposes() {
        string projectRootA = CreateProjectRoot();
        string projectRootB = CreateProjectRoot();
        EditorSession sessionA = null;
        EditorSession sessionB = null;

        try {
            sessionA = CreateActualEditorSession(projectRootA);
            AssetBrowserEntry cubeEntry = AssetBrowserEntry.CreateGeneratedAsset(
                "Cube",
                EngineGeneratedAssetProvider.CubeRelativePath,
                AssetEntryKind.Model,
                EngineGeneratedAssetProvider.ProviderIdValue,
                EngineGeneratedModelCache.CubeAssetId);
            RuntimeModel modelA = GetPrivateField<GeneratedAssetProviderRegistry>(sessionA, "generatedAssetProviderRegistry").ResolveRuntimeModel(cubeEntry);
            sessionB = CreateActualEditorSession(projectRootB);

            // Create A-owned scene state after B has become the ambient legacy
            // core. The explicit editor factory must still bind the entity to A.
            EditorEntity selectedEntityA = Assert.IsType<EditorEntity>(sessionA.Core.EntityFactory.Create("Session A entity"));
            CameraComponent cameraA = new CameraComponent();
            selectedEntityA.AddComponent(cameraA);
            sessionA.InteractionServices.Selection.SetSelectedEntity(selectedEntityA);
            sessionA.InteractionServices.InputCapture.SetBlocker(selectedEntityA, new int2(6, 7), new int2(14, 15));
            sessionA.InteractionServices.ViewportTool.SetToolMode(cameraA, EditorViewportToolMode.Rotate);
            sessionA.InteractionServices.GizmoHover.SetHoveredHandle(cameraA, selectedEntityA);
            sessionA.InteractionServices.GizmoDrag.BeginDrag(cameraA, selectedEntityA);
            sessionA.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowProperties);
            EditorWorkspacePanelInstance secondaryPropertiesPanel = Assert.Single(
                sessionA.GetPanelInstancesForTest("properties")
                    .Where(instance => !string.Equals(instance.InstanceId, "properties-primary", StringComparison.OrdinalIgnoreCase)));
            Assert.Same(sessionA.Core, secondaryPropertiesPanel.Dockable.OwnerCore);
            Assert.Same(sessionA.InteractionServices, secondaryPropertiesPanel.Dockable.InteractionServices);
            int focusTargetCountA = GetFocusTargetCount(sessionA.InteractionServices);
            Assert.True(focusTargetCountA > 0);
            string savedScenePath = Path.Combine(projectRootA, "assets", "Scenes", "IsolationAfterB.helen");
            GetPrivateField<SceneSaveService>(sessionA, "SceneSaveService").Save(savedScenePath);

            sessionB.Dispose();
            sessionB = null;

            string savedScenePathAfterPeerDispose = Path.Combine(projectRootA, "assets", "Scenes", "IsolationAfterPeerDispose.helen");
            GetPrivateField<SceneSaveService>(sessionA, "SceneSaveService").Save(savedScenePathAfterPeerDispose);
            Assert.Same(modelA, GetPrivateField<GeneratedAssetProviderRegistry>(sessionA, "generatedAssetProviderRegistry").ResolveRuntimeModel(cubeEntry));
            Assert.Same(selectedEntityA, sessionA.InteractionServices.Selection.SelectedEntity);
            Assert.Equal(focusTargetCountA, GetFocusTargetCount(sessionA.InteractionServices));
            Assert.True(sessionA.InteractionServices.InputCapture.IsPointerBlocked(new int2(8, 9)));
            Assert.Equal(EditorViewportToolMode.Rotate, sessionA.InteractionServices.ViewportTool.GetToolMode(cameraA));
            Assert.Same(selectedEntityA, sessionA.InteractionServices.GizmoHover.GetHoveredHandle(cameraA));
            Assert.True(sessionA.InteractionServices.GizmoDrag.IsDragging(cameraA));
            Assert.False(sessionA.Core.RenderManager2D.PixelTexture.IsDisposed);
            sessionA.Core.ObjectManager.Update();
            sessionA.Core.Update();
            sessionA.Core.Draw();
            Assert.True(Assert.IsType<TestDirectX11RenderManager3D>(sessionA.Core.RenderManager3D).DrawCallCount > 0);
            Assert.Same(sessionA.Core, secondaryPropertiesPanel.Dockable.OwnerCore);
            Assert.Same(sessionA.Core, sessionA.Core.RenderManager3D.OwnerCore);
            Assert.True(File.Exists(savedScenePath));
            Assert.True(File.Exists(savedScenePathAfterPeerDispose));
            sessionA.InteractionServices.GizmoDrag.EndDrag(cameraA);
            selectedEntityA.Dispose();
        } finally {
            sessionA?.Dispose();
            sessionB?.Dispose();
            DeleteProjectRoot(projectRootA);
            DeleteProjectRoot(projectRootB);
        }
    }

    [Fact]
    public void NestedCliCommand_ReusesOuterAuthoringGraphAndRejectsDifferentCanonicalRoot() {
        string projectRootA = CreateProjectRoot();
        string projectRootB = CreateProjectRoot();
        Core coreA = CreateCore(projectRootA);
        Core coreB = CreateCore(projectRootB);
        using TestGeneratedAssetGraph graphA = new TestGeneratedAssetGraph(coreA);
        graphA.Registry.Register(graphA.CreateProvider());
        using EditorProjectAuthoringSession authoringA = CreateAuthoringSession(projectRootA, graphA);
        ShaderBackendRegistry shaderBackends = CreateBackends();
        EditorCliCommandRunner commandRunner = new EditorCliCommandRunner(
            CreateEditorFont(),
            new EditorProjectAssetAuthoringServiceFactory(Array.Empty<IAssetImporterRegistration>()));

        try {
            EditorProjectBootstrapContext bootstrapA = CreateTestBootstrap(projectRootA);
            EditorProjectBootstrapContext bootstrapB = CreateTestBootstrap(projectRootB);

            // Core B is ambient by construction, while the nested command receives
            // A's authoring graph explicitly. A command id that is absent from the
            // empty project still traverses the real graph runner and returns its
            // structured command-resolution failure after graph setup.
            EditorBuildExecutionResult directoryResult = commandRunner.RunInSessionGraph(
                bootstrapA,
                new EditorCliCommandOptions(projectRootA, "test.graph.mutate"),
                authoringA,
                shaderBackends,
                coreA,
                graphA.InteractionServices,
                graphA.RendererResources,
                graphA.Registry);
            Assert.False(directoryResult.Succeeded);
            Assert.Contains("test.graph.mutate", directoryResult.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Same(coreB, Core.Instance);

            EditorBuildExecutionResult canonicalFileResult = commandRunner.RunInSessionGraph(
                bootstrapA,
                new EditorCliCommandOptions(Path.Combine(projectRootA, "project.heproj"), "test.graph.mutate"),
                authoringA,
                shaderBackends,
                coreA,
                graphA.InteractionServices,
                graphA.RendererResources,
                graphA.Registry);
            Assert.False(canonicalFileResult.Succeeded);
            Assert.Contains("test.graph.mutate", canonicalFileResult.Message, StringComparison.OrdinalIgnoreCase);

            InvalidOperationException differentRootException = Assert.Throws<InvalidOperationException>(() => commandRunner.RunInSessionGraph(
                bootstrapA,
                new EditorCliCommandOptions(Path.Combine(projectRootB, "project.heproj"), "test.graph.mutate"),
                authoringA,
                shaderBackends,
                coreA,
                graphA.InteractionServices,
                graphA.RendererResources,
                graphA.Registry));
            Assert.Contains("outer invocation project root", differentRootException.Message, StringComparison.OrdinalIgnoreCase);

            bootstrapA.BuildConfigService.Save(new EditorBuildConfigDocument {
                Platforms = new List<EditorBuildPlatformConfigDocument> {
                    new EditorBuildPlatformConfigDocument {
                        PlatformId = "windows",
                        SelectedBuildProfileId = "release",
                        EditorPrebuildCommandIdsByBuildProfileId = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase) {
                            ["release"] = new List<string> { "test.graph.mutate" }
                        }
                    }
                }
            });
            EditorBuildExecutionResult prebuildResult = new EditorCliBuildRunner(
                Array.Empty<IAssetImporterRegistration>(),
                CreateEditorFont())
                .ExecuteEditorPrebuildCommands(
                    bootstrapA,
                    new EditorCliBuildOptions(projectRootA, "windows", "release", projectRootA, false),
                    authoringA,
                    shaderBackends,
                    coreA,
                    graphA.InteractionServices,
                    graphA.RendererResources,
                    graphA.Registry);
            Assert.False(prebuildResult.Succeeded);
            Assert.Contains("test.graph.mutate", prebuildResult.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Same(coreB, Core.Instance);

            AssetBrowserEntry cubeEntry = AssetBrowserEntry.CreateGeneratedAsset(
                "Cube",
                EngineGeneratedAssetProvider.CubeRelativePath,
                AssetEntryKind.Model,
                EngineGeneratedAssetProvider.ProviderIdValue,
                EngineGeneratedModelCache.CubeAssetId);
            RuntimeModel modelAfterNestedRun = graphA.Registry.ResolveRuntimeModel(cubeEntry);
            Assert.Same(modelAfterNestedRun, graphA.Registry.ResolveRuntimeModel(cubeEntry));

            coreB.Dispose();
            coreB = null;
            Assert.Same(modelAfterNestedRun, graphA.Registry.ResolveRuntimeModel(cubeEntry));
        } finally {
            authoringA.Dispose();
            graphA.Dispose();
            coreA.Dispose();
            coreB?.Dispose();
            DeleteProjectRoot(projectRootA);
            DeleteProjectRoot(projectRootB);
        }
    }

    [Fact]
    public void CliCommand_ExecutesRealCommandAgainstOuterGeneratedAndInteractionGraph() {
        string projectRoot = CreateProjectRoot();
        Core core = CreateCore(projectRoot);
        string ambientProjectRoot = CreateProjectRoot();
        Core ambientCore = CreateCore(ambientProjectRoot);
        TestGeneratedAssetGraph graph = new TestGeneratedAssetGraph(core);
        graph.Registry.Register(graph.CreateProvider());
        EditorProjectAuthoringSession authoring = CreateAuthoringSession(projectRoot, graph);
        EditorCliCommandRunner runner = new EditorCliCommandRunner(
            CreateEditorFont(),
            new EditorProjectAssetAuthoringServiceFactory(Array.Empty<IAssetImporterRegistration>()));
        TestEditorScriptAssemblyHost commandCatalog = new TestEditorScriptAssemblyHost {
            AvailableEditorCommands = new[] {
                new EditorProjectCommandDescriptor(
                    "test.graph.mutate",
                    "Mutate Explicit Graph",
                    typeof(TestGraphMutationEditorCommand),
                    "test")
            }
        };

        try {
            TestGraphMutationEditorCommand.Reset();
            EditorBuildExecutionResult result = runner.RunInSessionGraph(
                CreateTestBootstrap(projectRoot),
                new EditorCliCommandOptions(Path.Combine(projectRoot, "project.heproj"), "test.graph.mutate"),
                authoring,
                CreateBackends(),
                core,
                graph.InteractionServices,
                graph.RendererResources,
                graph.Registry,
                commandCatalog,
                commandCatalog.ScriptTypeResolver);

            Assert.True(result.Succeeded, result.Message);
            Assert.Same(core, TestGraphMutationEditorCommand.LastCore);
            Assert.Same(graph.InteractionServices, TestGraphMutationEditorCommand.LastInteractionServices);
            Assert.Same(graph.RendererResources.WorldSpace2DPreviewMeshes.GetRuntimeModel(), TestGraphMutationEditorCommand.LastPreviewModel);
            Assert.Same(TestGraphMutationEditorCommand.LastEntity, graph.InteractionServices.Selection.SelectedEntity);
            Assert.True(graph.InteractionServices.InputCapture.IsPointerBlocked(new int2(4, 5)));
            Assert.Same(ambientCore, Core.Instance);
        } finally {
            TestGraphMutationEditorCommand.Reset();
            authoring.Dispose();
            graph.Dispose();
            ambientCore.Dispose();
            core.Dispose();
            DeleteProjectRoot(projectRoot);
            DeleteProjectRoot(ambientProjectRoot);
        }
    }

    [Fact]
    public void CliPrebuild_ExecutesRealNestedCommandWithoutReplacingOuterGraph() {
        string projectRoot = CreateProjectRoot();
        string ambientProjectRoot = CreateProjectRoot();
        Core core = CreateCore(projectRoot);
        Core ambientCore = CreateCore(ambientProjectRoot);
        TestGeneratedAssetGraph graph = new TestGeneratedAssetGraph(core);
        graph.Registry.Register(graph.CreateProvider());
        EditorProjectAuthoringSession authoring = CreateAuthoringSession(projectRoot, graph);
        EditorCliBuildRunner runner = new EditorCliBuildRunner(Array.Empty<IAssetImporterRegistration>(), CreateEditorFont());
        TestEditorScriptAssemblyHost commandCatalog = new TestEditorScriptAssemblyHost {
            AvailableEditorCommands = new[] {
                new EditorProjectCommandDescriptor("test.graph.mutate", "Mutate Explicit Graph", typeof(TestGraphMutationEditorCommand), "test")
            }
        };
        EditorProjectBootstrapContext bootstrap = CreateTestBootstrap(projectRoot);
        bootstrap.BuildConfigService.Save(new EditorBuildConfigDocument {
            Platforms = new List<EditorBuildPlatformConfigDocument> {
                new EditorBuildPlatformConfigDocument {
                    PlatformId = "windows",
                    SelectedBuildProfileId = "release",
                    EditorPrebuildCommandIdsByBuildProfileId = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase) {
                        ["release"] = new List<string> { "test.graph.mutate" }
                    }
                }
            }
        });

        try {
            TestGraphMutationEditorCommand.Reset();
            EditorBuildExecutionResult result = runner.ExecuteEditorPrebuildCommands(
                bootstrap,
                new EditorCliBuildOptions(projectRoot, "windows", "release", projectRoot, false),
                authoring,
                CreateBackends(),
                core,
                graph.InteractionServices,
                graph.RendererResources,
                graph.Registry,
                commandCatalog,
                commandCatalog.ScriptTypeResolver);

            Assert.True(result.Succeeded, result.Message);
            Assert.Same(core, TestGraphMutationEditorCommand.LastCore);
            Assert.Same(graph.InteractionServices, TestGraphMutationEditorCommand.LastInteractionServices);
            Assert.Same(ambientCore, Core.Instance);
            Assert.Same(TestGraphMutationEditorCommand.LastEntity, graph.InteractionServices.Selection.SelectedEntity);
        } finally {
            TestGraphMutationEditorCommand.Reset();
            authoring.Dispose();
            graph.Dispose();
            ambientCore.Dispose();
            core.Dispose();
            DeleteProjectRoot(projectRoot);
            DeleteProjectRoot(ambientProjectRoot);
        }
    }

    [Fact]
    public void EditorCommandContext_RejectsEveryMixedInvocationGraphDependency() {
        string projectRootA = CreateProjectRoot();
        string projectRootB = CreateProjectRoot();
        Core coreA = CreateCore(projectRootA);
        Core coreB = CreateCore(projectRootB);
        TestGeneratedAssetGraph graphA = new TestGeneratedAssetGraph(coreA);
        TestGeneratedAssetGraph graphB = new TestGeneratedAssetGraph(coreB);
        EditorProjectAuthoringSession authoringA = CreateAuthoringSession(projectRootA, graphA);
        ScriptTypeResolver resolver = new ScriptTypeResolver();

        try {
            Assert.Throws<InvalidOperationException>(() => new EditorCommandContext(
                projectRootA, resolver, authoringA, coreB, graphA.InteractionServices, graphA.Registry, graphA.RendererResources));
            Assert.Throws<InvalidOperationException>(() => new EditorCommandContext(
                projectRootA, resolver, authoringA, coreA, graphB.InteractionServices, graphA.Registry, graphA.RendererResources));
            Assert.Throws<InvalidOperationException>(() => new EditorCommandContext(
                projectRootA, resolver, authoringA, coreA, graphA.InteractionServices, graphB.Registry, graphA.RendererResources));
            Assert.Throws<InvalidOperationException>(() => new EditorCommandContext(
                projectRootA, resolver, authoringA, coreA, graphA.InteractionServices, graphA.Registry, graphB.RendererResources));
            EditorCommandContext matchingContext = new EditorCommandContext(
                projectRootA, resolver, authoringA, coreA, graphA.InteractionServices, graphA.Registry, graphA.RendererResources);
            Assert.Same(authoringA, matchingContext.Authoring);
        } finally {
            authoringA.Dispose();
            graphA.Dispose();
            graphB.Dispose();
            coreA.Dispose();
            coreB.Dispose();
            DeleteProjectRoot(projectRootA);
            DeleteProjectRoot(projectRootB);
        }
    }

    [Fact]
    public void EditorCommandContext_ValidatesCanonicalRootThroughAuthoringInterface() {
        string projectRootA = CreateProjectRoot();
        string projectRootB = CreateProjectRoot();
        Core coreA = CreateCore(projectRootA);
        TestGeneratedAssetGraph graphA = new TestGeneratedAssetGraph(coreA);
        EditorProjectAuthoringSession concreteAuthoring = CreateAuthoringSession(projectRootA, graphA);

        try {
            IEditorProjectAuthoringSession sameRootInterface = new AuthoringSessionProjection(concreteAuthoring, projectRootA);
            EditorCommandContext context = new EditorCommandContext(
                projectRootA,
                new ScriptTypeResolver(),
                sameRootInterface,
                coreA,
                graphA.InteractionServices,
                graphA.Registry,
                graphA.RendererResources);
            Assert.Same(sameRootInterface, context.Authoring);

            IEditorProjectAuthoringSession mismatchedRootInterface = new AuthoringSessionProjection(concreteAuthoring, projectRootB);
            Assert.Throws<InvalidOperationException>(() => new EditorCommandContext(
                projectRootA,
                new ScriptTypeResolver(),
                mismatchedRootInterface,
                coreA,
                graphA.InteractionServices,
                graphA.Registry,
                graphA.RendererResources));
        } finally {
            concreteAuthoring.Dispose();
            graphA.Dispose();
            coreA.Dispose();
            DeleteProjectRoot(projectRootA);
            DeleteProjectRoot(projectRootB);
        }
    }

    [Fact]
    public void EditorCommandContext_UsesPlatformSpecificProjectRootCaseComparison() {
        string projectRoot = CreateProjectRoot();
        Core core = CreateCore(projectRoot);
        TestGeneratedAssetGraph graph = new TestGeneratedAssetGraph(core);
        EditorProjectAuthoringSession concreteAuthoring = CreateAuthoringSession(projectRoot, graph);

        try {
            IEditorProjectAuthoringSession authoring = new AuthoringSessionProjection(concreteAuthoring, projectRoot);
            string caseVariantRoot = projectRoot.ToUpperInvariant();

            if (OperatingSystem.IsWindows()) {
                EditorCommandContext context = new EditorCommandContext(
                    caseVariantRoot,
                    new ScriptTypeResolver(),
                    authoring,
                    core,
                    graph.InteractionServices,
                    graph.Registry,
                    graph.RendererResources);
                Assert.Same(authoring, context.Authoring);
            } else {
                Assert.Throws<InvalidOperationException>(() => new EditorCommandContext(
                    caseVariantRoot,
                    new ScriptTypeResolver(),
                    authoring,
                    core,
                    graph.InteractionServices,
                    graph.Registry,
                    graph.RendererResources));
            }
        } finally {
            concreteAuthoring.Dispose();
            graph.Dispose();
            core.Dispose();
            DeleteProjectRoot(projectRoot);
        }
    }

    [Fact]
    public void LiveAuthoringSessions_KeepResolverGraphsIndependentAfterSessionADisposes() {
        string projectRootA = CreateProjectRoot();
        string projectRootB = CreateProjectRoot();
        Core coreA = CreateCore(projectRootA);
        Core coreB = CreateCore(projectRootB);
        TestGeneratedAssetGraph graphA = new TestGeneratedAssetGraph(coreA);
        TestGeneratedAssetGraph graphB = new TestGeneratedAssetGraph(coreB);
        graphA.Registry.Register(graphA.CreateProvider());
        graphB.Registry.Register(graphB.CreateProvider());

        try {
            using EditorProjectAuthoringSession sessionA = CreateAuthoringSession(projectRootA, graphA);
            using EditorProjectAuthoringSession sessionB = CreateAuthoringSession(projectRootB, graphB);
            ISceneAssetReferenceResolver resolverA = sessionA.CreateSceneAssetReferenceResolver();
            ISceneAssetReferenceResolver resolverB = sessionB.CreateSceneAssetReferenceResolver();

            RuntimeModel modelA = resolverA.ResolveModel(global::helengine.EngineSceneAssetReferenceFactory.CreateCubeModel());
            RuntimeModel modelB = resolverB.ResolveModel(global::helengine.EngineSceneAssetReferenceFactory.CreateCubeModel());

            Assert.NotSame(resolverA, resolverB);
            Assert.NotSame(modelA, modelB);

            sessionA.Dispose();
            graphA.Dispose();
            coreA.Dispose();

            RuntimeModel modelBAfterAClosed = resolverB.ResolveModel(global::helengine.EngineSceneAssetReferenceFactory.CreateCubeModel());
            Assert.Same(modelB, modelBAfterAClosed);
        } finally {
            graphA.Dispose();
            graphB.Dispose();
            coreA.Dispose();
            coreB.Dispose();
            DeleteProjectRoot(projectRootA);
            DeleteProjectRoot(projectRootB);
        }
    }

    [Fact]
    public void SeparateGraphs_DoNotShareGeneratedCachesOrProviderEntries() {
        Core coreA = CreateCore();
        Core coreB = CreateCore();
        using TestGeneratedAssetGraph graphA = new TestGeneratedAssetGraph(coreA);
        using TestGeneratedAssetGraph graphB = new TestGeneratedAssetGraph(coreB);
        EngineGeneratedModelCache modelCacheA = graphA.ModelCache;
        EngineGeneratedModelCache modelCacheB = graphB.ModelCache;
        EngineGeneratedMaterialCache materialCacheA = graphA.MaterialCache;
        EngineGeneratedMaterialCache materialCacheB = graphB.MaterialCache;
        GeneratedAssetProviderRegistry registryA = graphA.Registry;
        GeneratedAssetProviderRegistry registryB = graphB.Registry;
        registryA.Register(new EngineGeneratedAssetProvider(modelCacheA, materialCacheA));
        registryB.Register(new EngineGeneratedAssetProvider(modelCacheB, materialCacheB));

        AssetBrowserEntry modelEntry = AssetBrowserEntry.CreateGeneratedAsset(
            "Cube",
            EngineGeneratedAssetProvider.CubeRelativePath,
            AssetEntryKind.Model,
            EngineGeneratedAssetProvider.ProviderIdValue,
            EngineGeneratedModelCache.CubeAssetId);
        RuntimeModel modelA = registryA.ResolveRuntimeModel(modelEntry);
        RuntimeModel modelB = registryB.ResolveRuntimeModel(modelEntry);

        Assert.NotSame(modelA, modelB);
        Assert.Same(modelA, modelCacheA.GetRuntimeModel(EngineGeneratedModelCache.CubeAssetId));
        Assert.Same(modelB, modelCacheB.GetRuntimeModel(EngineGeneratedModelCache.CubeAssetId));

        registryA.Dispose();
        materialCacheA.Dispose();
        modelCacheA.Dispose();
        graphA.ShaderLibrary.Dispose();

        RuntimeModel modelBAfterAClosed = modelCacheB.GetRuntimeModel(EngineGeneratedModelCache.CubeAssetId);
        Assert.Same(modelB, modelBAfterAClosed);
        List<AssetBrowserEntry> entriesB = new List<AssetBrowserEntry>();
        registryB.Register(new TestGeneratedAssetProvider(
            "project-b",
            new[] { AssetBrowserEntry.CreateGeneratedDirectory("B", "B", "project-b") },
            new TestRuntimeModel()));
        registryB.LoadEntries(string.Empty, entriesB);
        Assert.Contains(entriesB, entry => entry.ProviderId == "project-b");

        coreA.Dispose();
        coreB.Dispose();
    }

    static ShaderBackendRegistry CreateBackends() {
        ShaderBackendRegistry registry = new ShaderBackendRegistry();
        registry.Register(new DirectX11ShaderBackend());
        registry.Register(new VulkanShaderBackend());
        return registry;
    }

    static Core CreateCore() {
        Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
        core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        return core;
    }

    static Core CreateCore(string projectRootPath) {
        Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(projectRootPath) });
        core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        return core;
    }

    static EditorSession CreateActualEditorSession(string projectRootPath) {
        EditorCore core = new EditorCore(new Project {
            Name = "Generated isolation",
            Path = projectRootPath
        });
        try {
            ShaderBackendRegistry shaderBackendRegistry = CreateBackends();
            EditorSession session = new EditorSession(
                core,
                Path.Combine(projectRootPath, "project.heproj"),
                new EditorPreferencesSettings(new EditorUiScaleSettings(EditorUiScaleMode.Override, 100), EditorThemeCatalog.DefaultThemeId),
                EditorUiMetrics.Default,
                CreateEditorFont(),
                CreateEditorFont(),
                TestDirectX11RenderManager3D.Create(),
                new TestRenderManager2D(),
                new TestInputBackend(),
                1280,
                720,
                CreateToolbarIcons(),
                CreateTexture(),
                Array.Empty<IAssetImporterRegistration>(),
                () => projectRootPath,
                shaderBackendRegistry,
                new AvailablePlatformProviderResolver(new PlatformDiscoveryOptions(projectRootPath)));
            return session;
        } catch {
            core.Dispose();
            throw;
        }
    }

    static EditorViewportToolbarIconSet CreateToolbarIcons() {
        return new EditorViewportToolbarIconSet(
            CreateTexture(),
            CreateTexture(),
            CreateTexture(),
            CreateTexture(),
            CreateTexture(),
            CreateTexture(),
            CreateTexture(),
            CreateTexture(),
            CreateTexture(),
            CreateTexture(),
            CreateTexture());
    }

    static RuntimeTexture CreateTexture() {
        return new TestRuntimeTexture {
            Width = 16,
            Height = 16
        };
    }

    static FontAsset CreateEditorFont() {
        Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
        const string glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?+-_[]()/'\\\\=<>";
        for (int index = 0; index < glyphs.Length; index++) {
            char glyph = glyphs[index];
            if (!characters.ContainsKey(glyph)) {
                float width = glyph == ' ' ? 4f : 8f;
                characters.Add(glyph, new FontChar(new float4(0f, 0f, width, 12f), 0f, width, 0f, 0f));
            }
        }

        return new FontAsset(
            new FontInfo("Generated isolation", 16, 4f),
            CreateTexture(),
            characters,
            16f,
            64,
            64);
    }

    static T GetPrivateField<T>(EditorSession session, string fieldName) {
        FieldInfo field = typeof(EditorSession).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return Assert.IsType<T>(field.GetValue(session));
    }

    static int GetFocusTargetCount(EditorSessionInteractionServices interactionServices) {
        FieldInfo field = typeof(EditorKeyboardFocusService).GetField("RegisteredTargets", BindingFlags.Instance | BindingFlags.NonPublic);
        return Assert.IsAssignableFrom<System.Collections.ICollection>(field.GetValue(interactionServices.KeyboardFocus)).Count;
    }

    static EditorProjectAuthoringSession CreateAuthoringSession(string projectRootPath, TestGeneratedAssetGraph graph) {
        return new EditorProjectAuthoringSession(
            projectRootPath,
            Array.Empty<IAssetImporterRegistration>(),
            new ContentManager(new HostFileSystemContentStreamSource(Path.Combine(projectRootPath, "assets"))),
            graph.Registry,
            graph.ModelCache,
            graph.MaterialCache,
            graph.RendererResources);
    }

    static string CreateProjectRoot() {
        string projectRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-isolation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(projectRootPath, "assets"));
        File.WriteAllText(
            Path.Combine(projectRootPath, "project.heproj"),
            "{\"projectFormatVersion\":1,\"name\":\"Generated isolation\",\"requiredEngineVersion\":\"0.4.0\",\"supportedPlatforms\":[\"windows\"],\"created\":\"2026-04-01T00:00:00Z\",\"lastOpened\":\"2026-04-20T00:00:00Z\",\"version\":\"1.0.0\"}");
        return projectRootPath;
    }

    static EditorProjectBootstrapContext CreateTestBootstrap(string projectRootPath) {
        ProjectFileDocument projectDocument = new ProjectFileDocument {
            Name = "Generated isolation",
            Version = "1.0.0",
            RequiredEngineVersion = "0.4.0",
            SupportedPlatforms = new List<string>()
        };
        return new EditorProjectBootstrapContext(
            Path.Combine(projectRootPath, "project.heproj"),
            projectRootPath,
            "project.heproj",
            projectDocument,
            projectDocument.SupportedPlatforms,
            Array.Empty<AvailablePlatformDescriptor>(),
            new AvailablePlatformProviderResolver(new PlatformDiscoveryOptions()),
            new EditorPlatformCatalogService(Array.Empty<AvailablePlatformDescriptor>()),
            new EditorProjectSceneCatalogService(projectRootPath),
            new EditorBuildConfigService(projectRootPath),
            new EditorProfileSettingsService(projectRootPath));
    }

    static void DeleteProjectRoot(string projectRootPath) {
        if (Directory.Exists(projectRootPath)) {
            Directory.Delete(projectRootPath, true);
        }
    }

    sealed class AuthoringSessionProjection : TestEditorProjectAuthoringSessionBase {
        readonly IEditorProjectAuthoringSession Inner;

        public AuthoringSessionProjection(IEditorProjectAuthoringSession inner, string projectRootPath) {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            ProjectRootPath = projectRootPath ?? throw new ArgumentNullException(nameof(projectRootPath));
        }

        public override string ProjectRootPath { get; }
        public override Core OwningCore => Inner.OwningCore;
        public override GeneratedAssetProviderRegistry GeneratedAssetProviders => Inner.GeneratedAssetProviders;
        public override EngineGeneratedModelCache GeneratedModelCache => Inner.GeneratedModelCache;
        public override EngineGeneratedMaterialCache GeneratedMaterialCache => Inner.GeneratedMaterialCache;
        public override EditorSessionRendererResources RendererResources => Inner.RendererResources;
        public override EditorAssetRepairReport RepairReport => Inner.RepairReport;

        public override SceneAssetReference CreateReference(string relativePath, AssetEntryKind expectedKind) => Inner.CreateReference(relativePath, expectedKind);
        public override AssetReferenceResolution ResolveReference(SceneAssetReference reference, AssetEntryKind expectedKind) => Inner.ResolveReference(reference, expectedKind);
        public override RuntimeModel LoadImportedRuntimeModel(string relativePath) => Inner.LoadImportedRuntimeModel(relativePath);
        public override EditorAssetWriteResult WriteAsset(string relativePath, Asset asset) => Inner.WriteAsset(relativePath, asset);
        public override EditorAuthoringTransaction BeginTransaction() => Inner.BeginTransaction();
        public override void RefreshExternalChanges() => Inner.RefreshExternalChanges();
        public void Dispose() {
        }
    }
}
