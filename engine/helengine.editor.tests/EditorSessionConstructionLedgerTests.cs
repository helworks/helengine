using System.Runtime.CompilerServices;
using System.Reflection;
using helengine.directx11;
using Xunit;
using helengine.ui;
using helengine.editor.tests.testing;
using helengine.vulkan;

namespace helengine.editor.tests;

/// <summary>
/// Verifies that editor construction ownership remains available for the normal
/// session teardown after construction succeeds.
/// </summary>
public sealed class EditorSessionConstructionLedgerTests {
    [Fact]
    public void TransferOwnership_WithOwnerCleanup_RetainsCleanupForSessionDispose() {
        EditorSessionConstructionLedger ledger = new EditorSessionConstructionLedger();
        int disposeCount = 0;
        ledger.Register(() => disposeCount++);

        ledger.TransferOwnership();
        ledger.Dispose();
        ledger.Dispose();

        Assert.Equal(1, disposeCount);
    }

    [Fact]
    public void TransferOwnership_WithMiddleFailure_RetriesOnlyTheFailedEntry() {
        EditorSessionConstructionLedger ledger = new EditorSessionConstructionLedger();
        List<string> calls = new List<string>();
        bool failOnce = true;
        ledger.Register(() => calls.Add("first"));
        ledger.Register(() => {
            calls.Add("middle");
            if (failOnce) {
                failOnce = false;
                throw new InvalidOperationException("middle failed once");
            }
        });
        ledger.Register(() => calls.Add("last"));
        ledger.TransferOwnership();

        Assert.Throws<InvalidOperationException>(() => ledger.Dispose());
        ledger.Dispose();
        ledger.Dispose();

        Assert.Equal(new[] { "last", "middle", "first", "middle" }, calls);
    }

    [Fact]
    public void Dispose_RunsDetachPhaseBeforeResetAndDisposePhases() {
        EditorSessionConstructionLedger ledger = new EditorSessionConstructionLedger();
        List<string> calls = new List<string>();
        ledger.Register(() => calls.Add("publisher"), EditorSessionCleanupPhase.Dispose);
        ledger.Register(() => calls.Add("reset"), EditorSessionCleanupPhase.Reset);
        ledger.Register(() => calls.Add("detacher"), EditorSessionCleanupPhase.Detach);
        ledger.TransferOwnership();

        ledger.Dispose();

        Assert.Equal(new[] { "detacher", "reset", "publisher" }, calls);
    }

    [Fact]
    public void Dispose_WhenDetachFails_RetriesOnlyTheUnresolvedDetacher() {
        EditorSessionConstructionLedger ledger = new EditorSessionConstructionLedger();
        List<string> calls = new List<string>();
        bool failOnce = true;
        ledger.Register(() => calls.Add("publisher"), EditorSessionCleanupPhase.Dispose);
        ledger.Register(() => {
            calls.Add("first-detacher");
            if (failOnce) {
                failOnce = false;
                throw new InvalidOperationException("detacher failed once");
            }
        }, EditorSessionCleanupPhase.Detach);
        ledger.Register(() => calls.Add("second-detacher"), EditorSessionCleanupPhase.Detach);
        ledger.TransferOwnership();

        Assert.Throws<InvalidOperationException>(() => ledger.Dispose());
        ledger.Dispose();
        ledger.Dispose();

        Assert.Equal(
            new[] { "second-detacher", "first-detacher", "first-detacher", "publisher" },
            calls);
    }

    [Fact]
    public void Dispose_WhenMultipleActionsFail_PreservesEveryFailureForInspection() {
        EditorSessionConstructionLedger ledger = new EditorSessionConstructionLedger();
        ledger.Register(() => throw new InvalidOperationException("first cleanup"));
        ledger.Register(() => throw new ArgumentException("second cleanup"));
        ledger.TransferOwnership();

        AggregateException exception = Assert.Throws<AggregateException>(() => ledger.Dispose());

        Assert.Equal(2, exception.Flatten().InnerExceptions.Count);
        Assert.Contains(exception.Flatten().InnerExceptions, failure => failure.Message == "first cleanup");
        Assert.Contains(exception.Flatten().InnerExceptions, failure => failure.Message == "second cleanup");
    }

    [Fact]
    public void LiveDispose_WhenHighPhaseFails_AttemptsSiblingsButBlocksLowerPhasesUntilRetry() {
        EditorSessionConstructionLedger ledger = new EditorSessionConstructionLedger();
        List<string> calls = new List<string>();
        bool failOnce = true;
        ledger.Register(() => calls.Add("lower"), EditorSessionCleanupPhase.Dispose);
        ledger.Register(() => {
            calls.Add("high-failing");
            if (failOnce) {
                failOnce = false;
                throw new InvalidOperationException("high phase failed once");
            }
        }, EditorSessionCleanupPhase.Panel);
        ledger.Register(() => calls.Add("high-sibling"), EditorSessionCleanupPhase.Panel);
        ledger.TransferOwnership();

        Assert.Throws<InvalidOperationException>(() => ledger.Dispose());
        Assert.Equal(new[] { "high-sibling", "high-failing" }, calls);

        ledger.Dispose();
        Assert.Equal(new[] { "high-sibling", "high-failing", "high-failing", "lower" }, calls);

        ledger.Dispose();
        Assert.Equal(new[] { "high-sibling", "high-failing", "high-failing", "lower" }, calls);
    }

    [Fact]
    public void ConstructionAbort_WhenMultiplePhasesFail_AttemptsEveryPhase() {
        EditorSessionConstructionLedger ledger = new EditorSessionConstructionLedger();
        List<string> calls = new List<string>();
        ledger.Register(() => {
            calls.Add("lower");
            throw new InvalidOperationException("lower cleanup");
        }, EditorSessionCleanupPhase.Dispose);
        ledger.Register(() => {
            calls.Add("higher");
            throw new InvalidOperationException("higher cleanup");
        }, EditorSessionCleanupPhase.Panel);

        AggregateException failure = Assert.Throws<AggregateException>(() => ledger.Dispose(EditorSessionCleanupMode.ConstructionAbort));

        Assert.Equal(new[] { "higher", "lower" }, calls);
        Assert.Contains(failure.Flatten().InnerExceptions, exception => exception.Message == "higher cleanup");
        Assert.Contains(failure.Flatten().InnerExceptions, exception => exception.Message == "lower cleanup");
    }

    [Fact]
    public void LiveDispose_WhenOwnedSceneEntityCleanupFails_PreservesTeardownOrderAndBlocksAssetRelease() {
        EditorSessionConstructionLedger ledger = new EditorSessionConstructionLedger();
        List<string> calls = new List<string>();
        bool failOnce = true;

        // Registration is intentionally in the reverse of the desired live
        // teardown order because the ledger executes each phase LIFO.
        ledger.Register(() => calls.Add("release-assets"), EditorSessionCleanupPhase.OwnedState);
        ledger.Register(() => calls.Add("flush-pending-releases"), EditorSessionCleanupPhase.OwnedPendingAssetFlush);
        ledger.Register(() => {
            calls.Add("dispose-scene-entities");
            if (failOnce) {
                failOnce = false;
                throw new InvalidOperationException("scene entity cleanup failed once");
            }
        }, EditorSessionCleanupPhase.OwnedSceneEntities);
        ledger.Register(() => calls.Add("untrack-scene"), EditorSessionCleanupPhase.OwnedSceneUntrack);
        ledger.TransferOwnership();

        Assert.Throws<InvalidOperationException>(() => ledger.Dispose());
        Assert.Equal(new[] { "untrack-scene", "dispose-scene-entities" }, calls);

        ledger.Dispose();
        Assert.Equal(
            new[] { "untrack-scene", "dispose-scene-entities", "dispose-scene-entities", "flush-pending-releases", "release-assets" },
            calls);

        ledger.Dispose();
        Assert.Equal(
            new[] { "untrack-scene", "dispose-scene-entities", "dispose-scene-entities", "flush-pending-releases", "release-assets" },
            calls);
    }

    [Fact]
    public void InitializeAssetImports_SourceUsesFailureAtomicOwnerCleanup() {
        string sourcePath = ResolveSourcePath("EditorSession.cs");
        string source = File.ReadAllText(sourcePath);
        int methodStart = source.IndexOf("AssetImportManager InitializeAssetImports(", StringComparison.Ordinal);
        Assert.True(methodStart >= 0);
        int methodEnd = source.IndexOf("\n        }", methodStart, StringComparison.Ordinal);
        Assert.True(methodEnd > methodStart);
        string method = source.Substring(methodStart, methodEnd - methodStart);

        Assert.Contains("try", method, StringComparison.Ordinal);
        Assert.Contains("manager.Dispose()", method, StringComparison.Ordinal);
        Assert.Contains("projectContentManager.Dispose()", method, StringComparison.Ordinal);
    }

    [Fact]
    public void SessionConstruction_SourceDoesNotRegisterAggregateDialogOrSubscriptionCleanup() {
        string sourcePath = ResolveSourcePath("EditorSession.cs");
        string source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("constructionLedger.Register(DisposeScaleSensitiveDialogs", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ledger.Register(DetachConstructionSubscriptions", source, StringComparison.Ordinal);
        Assert.Contains("RegisterScaleSensitiveDialogCleanup", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SessionConstruction_SourceUsesRetryableSceneOwnershipCleanup() {
        string sourcePath = ResolveSourcePath("EditorSession.cs");
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("DisposeUserSceneEntitiesForTeardown", source, StringComparison.Ordinal);
        Assert.DoesNotContain("constructionLedger.Register(ClearUserSceneEntities", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ledger.Register(ClearUserSceneEntities", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Dispose_WhenMiddlePanelActionFails_RetriesOnlyThatPanel() {
        EditorSessionConstructionLedger ledger = new EditorSessionConstructionLedger();
        List<string> calls = new List<string>();
        bool failOnce = true;
        ledger.Register(() => calls.Add("first"), EditorSessionCleanupPhase.Panel);
        ledger.Register(() => {
            calls.Add("middle");
            if (failOnce) {
                failOnce = false;
                throw new InvalidOperationException("middle panel failed once");
            }
        }, EditorSessionCleanupPhase.Panel);
        ledger.Register(() => calls.Add("last"), EditorSessionCleanupPhase.Panel);
        ledger.TransferOwnership();

        Assert.Throws<InvalidOperationException>(() => ledger.Dispose());
        ledger.Dispose();
        ledger.Dispose();

        Assert.Equal(new[] { "last", "middle", "first", "middle" }, calls);
    }

    [Fact]
    public void DisposeScaleSensitiveDialogs_WhenMiddleDialogFails_RetriesOnlyThatDialog() {
        EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
        EditorSessionConstructionLedger ledger = new EditorSessionConstructionLedger();
        List<string> calls = new List<string>();
        bool failOnce = true;
        MethodInfo register = typeof(EditorSession).GetMethod("RegisterScaleSensitiveDialogCleanup", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo dispose = typeof(EditorSession).GetMethod("DisposeScaleSensitiveDialogs", BindingFlags.Instance | BindingFlags.NonPublic);

        register.Invoke(session, new object[] { ledger, (Action)(() => calls.Add("first")), null, null });
        register.Invoke(session, new object[] { ledger, (Action)(() => {
            calls.Add("middle");
            if (failOnce) {
                failOnce = false;
                throw new InvalidOperationException("dialog failed once");
            }
        }), null, null });
        register.Invoke(session, new object[] { ledger, (Action)(() => calls.Add("last")), null, null });

        Assert.Throws<TargetInvocationException>(() => dispose.Invoke(session, null));
        dispose.Invoke(session, null);
        dispose.Invoke(session, null);

        Assert.Equal(new[] { "first", "middle", "last", "middle" }, calls);
    }

    [Fact]
    public void DetachScaleSensitiveDialogHandlers_WhenMiddleDetacherFails_RetriesOnlyThatDetacher() {
        EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
        typeof(EditorSession).GetField("ScaleSensitiveDialogDetacherItems", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(
            session,
            new List<EditorSessionCleanupItem>());
        typeof(EditorSession).GetField("RegisteringScaleSensitiveDialogHandlers", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, true);
        EditorSessionConstructionLedger ledger = new EditorSessionConstructionLedger();
        List<string> calls = new List<string>();
        bool failOnce = true;
        MethodInfo register = typeof(EditorSession).GetMethod("RegisterDetacher", BindingFlags.Instance | BindingFlags.NonPublic);
        register.Invoke(session, new object[] { ledger, (Action)(() => calls.Add("first")) });
        register.Invoke(session, new object[] { ledger, (Action)(() => {
            calls.Add("middle");
            if (failOnce) {
                failOnce = false;
                throw new InvalidOperationException("detacher failed once");
            }
        }) });
        register.Invoke(session, new object[] { ledger, (Action)(() => calls.Add("last")) });
        typeof(EditorSession).GetField("RegisteringScaleSensitiveDialogHandlers", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, false);

        MethodInfo detach = typeof(EditorSession).GetMethod("DetachScaleSensitiveDialogHandlers", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.Throws<TargetInvocationException>(() => detach.Invoke(session, null));
        detach.Invoke(session, null);
        detach.Invoke(session, null);

        Assert.Equal(new[] { "first", "middle", "last", "middle" }, calls);
    }

    [Theory]
    [InlineData("after-core-acquired", 1)]
    [InlineData("after-primary-viewport-acquired", 2)]
    [InlineData("mid-construction", 3)]
    [InlineData("after-first-subscription", 4)]
    [InlineData("after-shader-package-initialized", 5)]
    [InlineData("late", 6)]
    public void RealSessionConstructionFailure_AtEveryCheckpointCleansReachableState(string failureCheckpoint, int expectedCheckpointCount) {
        string projectRoot = Path.Combine(Path.GetTempPath(), "helengine-session-checkpoints-", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "assets"));
        string projectFilePath = Path.Combine(projectRoot, "project.heproj");
        File.WriteAllText(projectFilePath, "{\"projectFormatVersion\":1,\"name\":\"Checkpoint\",\"requiredEngineVersion\":\"0.4.0\",\"supportedPlatforms\":[\"windows\"],\"created\":\"2026-04-01T00:00:00Z\",\"lastOpened\":\"2026-04-20T00:00:00Z\",\"version\":\"1.0.0\"}");
        List<string> checkpoints = new List<string>();
        EditorSession.ConstructionCheckpointForTests = checkpoint => {
            checkpoints.Add(checkpoint);
            if (string.Equals(checkpoint, failureCheckpoint, StringComparison.Ordinal)) {
                throw new InvalidOperationException("checkpoint failure: " + checkpoint);
            }
        };

        try {
            Exception failure = Record.Exception(() => CreateRealSessionForCheckpoint(projectRoot, projectFilePath));

            Assert.NotNull(failure);
            Assert.Equal(expectedCheckpointCount, checkpoints.Count);
            Assert.Equal(
                new[] {
                    "after-core-acquired",
                    "after-primary-viewport-acquired",
                    "mid-construction",
                    "after-first-subscription",
                    "after-shader-package-initialized",
                    "late"
                }.Take(expectedCheckpointCount),
                checkpoints);
            Assert.Null(EditorEntityHistoryMutationService.CaptureEntityState);
            Assert.Null(EditorEntityHistoryMutationService.RecordEntityStateChange);
            Assert.Null(EditorComponentHistoryMutationService.CaptureEntityState);
            Assert.Null(EditorComponentHistoryMutationService.RecordComponentMutation);
            Assert.False(EditorInputCaptureService.IsPointerBlocked(new int2(3, 3)));
            Assert.Null(EditorGizmoHoverService.HoveredHandleEntity);
            Assert.False(EditorGizmoDragService.IsDragging(new CameraComponent()));
        } finally {
            EditorSession.ConstructionCheckpointForTests = null;
            EditorEntityHistoryMutationService.Reset();
            EditorComponentHistoryMutationService.Reset();
            EditorInputCaptureService.Reset();
            EditorGizmoHoverService.Reset();
            EditorGizmoDragService.Reset();
            if (Directory.Exists(projectRoot)) {
                Directory.Delete(projectRoot, true);
            }
        }
    }

    [Fact]
    public void RealSessionDispose_WithTwoDynamicPanels_RetriesOnlyFailedController() {
        string projectRoot = Path.Combine(Path.GetTempPath(), "helengine-session-panels-", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "assets"));
        string projectFilePath = Path.Combine(projectRoot, "project.heproj");
        File.WriteAllText(projectFilePath, "{\"projectFormatVersion\":1,\"name\":\"Panel cleanup\",\"requiredEngineVersion\":\"0.4.0\",\"supportedPlatforms\":[\"windows\"],\"created\":\"2026-04-01T00:00:00Z\",\"lastOpened\":\"2026-04-20T00:00:00Z\",\"version\":\"1.0.0\"}");
        List<FailOnceWorkspaceController> controllers = new List<FailOnceWorkspaceController>();
        EditorSession.WorkspacePanelControllerDecoratorForTests = controller => {
            FailOnceWorkspaceController decorated = new FailOnceWorkspaceController(controller, controllers.Count == 1);
            controllers.Add(decorated);
            return decorated;
        };

        EditorSession session = null;
        try {
            session = CreateRealSessionForCheckpoint(projectRoot, projectFilePath);
            MethodInfo createPanel = typeof(EditorSession).GetMethod("CreateWorkspacePanelInstance", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
            Assert.NotNull(createPanel);
            createPanel.Invoke(session, new object[] { "logger" });
            createPanel.Invoke(session, new object[] { "logger" });
            Assert.Equal(2, controllers.Count);

            Exception firstFailure = Record.Exception(session.Dispose);
            Assert.NotNull(firstFailure);
            Assert.Equal(new[] { 1, 1 }, controllers.Select(controller => controller.DisposeCount));
            Assert.False((bool)typeof(EditorSession).GetField("IsDisposed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session));

            Exception secondFailure = Record.Exception(session.Dispose);
            Assert.Null(secondFailure);
            Assert.Equal(new[] { 1, 2 }, controllers.Select(controller => controller.DisposeCount));
            Assert.True((bool)typeof(EditorSession).GetField("IsDisposed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session));

            session.Dispose();
            Assert.Equal(new[] { 1, 2 }, controllers.Select(controller => controller.DisposeCount));
        } finally {
            EditorSession.WorkspacePanelControllerDecoratorForTests = null;
            if (session != null) {
                Record.Exception(session.Dispose);
            }
            if (Directory.Exists(projectRoot)) {
                Directory.Delete(projectRoot, true);
            }
        }
    }

    [Fact]
    public void RealSessionConstructionFailure_AtLateCheckpoint_PreservesPrimaryAndCleanupFailures() {
        string projectRoot = Path.Combine(Path.GetTempPath(), "helengine-session-failures-", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "assets"));
        string projectFilePath = Path.Combine(projectRoot, "project.heproj");
        File.WriteAllText(projectFilePath, "{\"projectFormatVersion\":1,\"name\":\"Failure inspection\",\"requiredEngineVersion\":\"0.4.0\",\"supportedPlatforms\":[\"windows\"],\"created\":\"2026-04-01T00:00:00Z\",\"lastOpened\":\"2026-04-20T00:00:00Z\",\"version\":\"1.0.0\"}");
        EditorSession.ConstructionCheckpointForTests = checkpoint => {
            if (checkpoint == "late") {
                throw new InvalidOperationException("primary construction failure");
            }
        };
        EditorSession.DisposalCheckpointForTests = sequence => {
            if (sequence == 0) {
                throw new InvalidOperationException("first cleanup failure");
            }
            if (sequence == 1) {
                throw new InvalidOperationException("second cleanup failure");
            }
        };

        try {
            AggregateException failure = Assert.IsType<AggregateException>(Record.Exception(() => CreateRealSessionForCheckpoint(projectRoot, projectFilePath)));
            IReadOnlyList<Exception> failures = failure.Flatten().InnerExceptions;
            Assert.Contains(failures, exception => exception.Message == "primary construction failure");
            Assert.Contains(failures, exception => exception.Message == "first cleanup failure");
            Assert.Contains(failures, exception => exception.Message == "second cleanup failure");
        } finally {
            EditorSession.ConstructionCheckpointForTests = null;
            EditorSession.DisposalCheckpointForTests = null;
            EditorEntityHistoryMutationService.Reset();
            EditorComponentHistoryMutationService.Reset();
            EditorInputCaptureService.Reset();
            EditorGizmoHoverService.Reset();
            EditorGizmoDragService.Reset();
            if (Directory.Exists(projectRoot)) {
                Directory.Delete(projectRoot, true);
            }
        }
    }

    sealed class FailOnceWorkspaceController : IEditorWorkspacePanelController {
        readonly IEditorWorkspacePanelController Inner;
        bool FailOnce;

        internal FailOnceWorkspaceController(IEditorWorkspacePanelController inner, bool failOnce) {
            Inner = inner;
            FailOnce = failOnce;
        }

        public DockableEntity Dockable => Inner.Dockable;

        public int DisposeCount { get; private set; }

        public object CaptureState() {
            return Inner.CaptureState();
        }

        public void RestoreState(object state) {
            Inner.RestoreState(state);
        }

        public void Dispose() {
            DisposeCount++;
            if (FailOnce) {
                FailOnce = false;
                throw new InvalidOperationException("dynamic panel cleanup failed once");
            }

            Inner.Dispose();
        }
    }

    static EditorSession CreateRealSessionForCheckpoint(string projectRoot, string projectFilePath) {
        EditorCore core = new EditorCore(new Project {
            Name = "Checkpoint",
            Path = projectRoot
        });
        ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
        shaderBackendRegistry.Register(new DirectX11ShaderBackend());
        shaderBackendRegistry.Register(new VulkanShaderBackend());
        return new EditorSession(
            core,
            projectFilePath,
            new EditorPreferencesSettings(new EditorUiScaleSettings(EditorUiScaleMode.Override, 100), EditorThemeCatalog.DefaultThemeId),
            EditorUiMetrics.Default,
            CreateCheckpointFont(),
            CreateCheckpointFont(),
            TestDirectX11RenderManager3D.Create(),
            new TestRenderManager2D(),
            new TestInputBackend(),
            1280,
            720,
            CreateCheckpointToolbarIcons(),
            CreateCheckpointTexture(),
            Array.Empty<IAssetImporterRegistration>(),
            () => projectRoot,
            shaderBackendRegistry);
    }

    static FontAsset CreateCheckpointFont() {
        Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
        const string glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .:-_[]+()/*=,'";
        for (int index = 0; index < glyphs.Length; index++) {
            char glyph = glyphs[index];
            if (!characters.ContainsKey(glyph)) {
                float width = glyph == ' ' ? 4f : 8f;
                characters.Add(glyph, new FontChar(new float4(0f, 0f, width, 12f), 0f, width, 0f, 0f));
            }
        }

        return new FontAsset(
            new FontInfo("Checkpoint", 16, 4f),
            CreateCheckpointTexture(),
            characters,
            16f,
            64,
            64);
    }

    static EditorViewportToolbarIconSet CreateCheckpointToolbarIcons() {
        return new EditorViewportToolbarIconSet(
            CreateCheckpointTexture(),
            CreateCheckpointTexture(),
            CreateCheckpointTexture(),
            CreateCheckpointTexture(),
            CreateCheckpointTexture(),
            CreateCheckpointTexture(),
            CreateCheckpointTexture(),
            CreateCheckpointTexture(),
            CreateCheckpointTexture(),
            CreateCheckpointTexture(),
            CreateCheckpointTexture());
    }

    static RuntimeTexture CreateCheckpointTexture() {
        return new TestRuntimeTexture {
            Width = 16,
            Height = 16
        };
    }

    [Fact]
    public void InitializeAssetImports_WhenRegistrationAndManagerCleanupFail_CleansContentAndPreservesBothFailures() {
        string projectRoot = Path.Combine(Path.GetTempPath(), "helengine-import-init-", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "assets"));
        string projectFilePath = Path.Combine(projectRoot, "project.heproj");
        File.WriteAllText(projectFilePath, "{\"projectFormatVersion\":1,\"name\":\"Import Init\",\"requiredEngineVersion\":\"0.4.0\",\"supportedPlatforms\":[\"windows\"],\"created\":\"2026-04-01T00:00:00Z\",\"lastOpened\":\"2026-04-20T00:00:00Z\",\"version\":\"1.0.0\"}");
        TrackingContentManager contentManager = null;
        TrackingAssetImportManager assetImportManager = null;
        EditorSession.ContentManagerFactoryForTests = assetsRootPath => {
            contentManager = new TrackingContentManager(new HostFileSystemContentStreamSource(assetsRootPath));
            return contentManager;
        };
        EditorSession.AssetImportManagerFactoryForTests = (rootPath, suppliedContentManager) => {
            assetImportManager = new TrackingAssetImportManager(rootPath, suppliedContentManager);
            return assetImportManager;
        };

        try {
            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            typeof(EditorSession).GetField("projectPath", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, projectFilePath);
            typeof(EditorSession).GetField("ActiveProjectPlatform", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, "windows");
            MethodInfo initialize = typeof(EditorSession).GetMethod("InitializeAssetImports", BindingFlags.Instance | BindingFlags.NonPublic);

            TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(() => initialize.Invoke(session, new object[] {
                new IAssetImporterRegistration[] { new ThrowingImporterRegistration() }
            }));
            Assert.NotNull(assetImportManager);
            Assert.NotNull(contentManager);
            AggregateException failure = Assert.IsType<AggregateException>(invocation.InnerException);

            Assert.Contains(failure.Flatten().InnerExceptions, exception => exception.Message == "registration failed");
            Assert.Contains(failure.Flatten().InnerExceptions, exception => exception.Message == "manager cleanup failed");
            Assert.Equal(1, assetImportManager.DisposeCount);
            Assert.Equal(1, contentManager.DisposeCount);
        } finally {
            EditorSession.ContentManagerFactoryForTests = null;
            EditorSession.AssetImportManagerFactoryForTests = null;
            if (Directory.Exists(projectRoot)) {
                Directory.Delete(projectRoot, true);
            }
        }
    }

    sealed class ThrowingImporterRegistration : IAssetImporterRegistration {
        public void Register(AssetImportManager manager) {
            throw new InvalidOperationException("registration failed");
        }
    }

    sealed class TrackingContentManager : ContentManager {
        public int DisposeCount { get; private set; }

        public TrackingContentManager(IContentStreamSource streamSource)
            : base(streamSource) {
        }

        public override void Dispose() {
            DisposeCount++;
        }
    }

    sealed class TrackingAssetImportManager : AssetImportManager {
        public int DisposeCount { get; private set; }

        public TrackingAssetImportManager(string projectRootPath, ContentManager contentManager)
            : base(projectRootPath, contentManager) {
        }

        public override void Dispose() {
            DisposeCount++;
            throw new InvalidOperationException("manager cleanup failed");
        }
    }

    static string ResolveSourcePath(string fileName) {
        DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null) {
            string candidate = Path.Combine(current.FullName, "helengine.editor", fileName);
            if (File.Exists(candidate)) {
                return candidate;
            }
            current = current.Parent;
        }

        throw new FileNotFoundException(fileName);
    }

    [Fact]
    public void Dispose_AttemptsLaterActionsAndRetainsFailedActionForRetry() {
        EditorSessionConstructionLedger ledger = new EditorSessionConstructionLedger();
        List<string> calls = new List<string>();
        bool failFirstAttempt = true;
        ledger.Register(() => {
            calls.Add("later");
        });
        ledger.Register(() => {
            calls.Add("retry");
            if (failFirstAttempt) {
                failFirstAttempt = false;
                throw new InvalidOperationException("fail once");
            }
        });

        Assert.Throws<InvalidOperationException>(() => ledger.Dispose());
        ledger.Dispose();

        Assert.Equal(new[] { "retry", "later", "retry" }, calls);
    }

    [Fact]
    public void ConstructorFailure_BeforeCoreInitialization_ResetsPreexistingInteractionState() {
        object blockerOwner = new object();
        EditorInputCaptureService.SetBlocker(blockerOwner, new int2(0, 0), new int2(10, 10));
        EditorSelectionService.Reset();
        EditorAssetPickerService.Reset();
        EditorEntityHistoryMutationService.Reset();
        EditorComponentHistoryMutationService.Reset();
        EditorCore core = new EditorCore(new Project {
            Name = "Construction failure",
            Path = Path.GetTempPath()
        });
        core.Initialize(null, new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        CameraComponent camera = new CameraComponent();
        EditorEntity handle = new EditorEntity();
        EditorGizmoHoverService.SetHoveredHandle(camera, handle);
        EditorGizmoDragService.BeginDrag(camera, handle);
        EditorViewportToolService.SetToolMode(camera, EditorViewportToolMode.Rotate);
        TransformGizmoSnapSettingsService.SetSnapValue(camera, EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap1, 99d);
        EditorSelectionService.SetSelectedEntity(handle);
        int pickerCalls = 0;
        Action<AssetPickerRequest> picker = _ => pickerCalls++;
        EditorAssetPickerService.PickRequested += picker;
        EditorEntityHistoryMutationService.CaptureEntityState = _ => null;
        EditorComponentHistoryMutationService.CaptureEntityState = _ => null;
        EditorSession.ConstructionCheckpointForTests = checkpoint => {
            if (checkpoint == "after-core-acquired") {
                throw new InvalidOperationException("injected construction failure");
            }
        };

        try {
            Assert.Throws<InvalidOperationException>(() => new EditorSession(
                core,
                string.Empty,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                1,
                1,
                null,
                null,
                null,
                null,
                null));
        } finally {
            EditorSession.ConstructionCheckpointForTests = null;
            EditorAssetPickerService.PickRequested -= picker;
            EditorInputCaptureService.Reset();
            EditorGizmoHoverService.Reset();
            EditorGizmoDragService.Reset();
            EditorViewportToolService.Reset();
            TransformGizmoSnapSettingsService.ResetDefaults();
            EditorSelectionService.Reset();
            EditorEntityHistoryMutationService.Reset();
            EditorComponentHistoryMutationService.Reset();
            handle.Dispose();
            core.Dispose();
        }

        Assert.False(EditorInputCaptureService.IsPointerBlocked(new int2(5, 5)));
        Assert.Null(EditorGizmoHoverService.HoveredHandleEntity);
        Assert.False(EditorGizmoDragService.IsDragging(camera));
        Assert.Equal(EditorViewportToolMode.Translate, EditorViewportToolService.GetToolMode(camera));
        Assert.Equal(5d, TransformGizmoSnapSettingsService.GetSnapValue(camera, EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap1));
        Assert.Null(EditorSelectionService.SelectedEntity);
        EditorAssetPickerService.RequestPick(_ => { });
        Assert.Equal(0, pickerCalls);
        Assert.Null(EditorEntityHistoryMutationService.CaptureEntityState);
        Assert.Null(EditorComponentHistoryMutationService.CaptureEntityState);
    }

    [Fact]
    public void Dispose_WhenFirstAggregateActionFails_StillRunsLaterInteractionResets() {
        EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
        object blockerOwner = new object();
        EditorInputCaptureService.SetBlocker(blockerOwner, new int2(0, 0), new int2(10, 10));
        bool injected = false;
        EditorSession.DisposalCheckpointForTests = sequence => {
            if (sequence == 0) {
                injected = true;
                throw new InvalidOperationException("injected teardown failure");
            }
        };

        try {
            Assert.NotNull(Record.Exception(session.Dispose));
        } finally {
            EditorSession.DisposalCheckpointForTests = null;
            EditorInputCaptureService.Reset();
        }

        Assert.True(injected);
        Assert.False(EditorInputCaptureService.IsPointerBlocked(new int2(5, 5)));
    }

    [Fact]
    public void InteractionStateReset_ClearsHoverDragToolAndSnapState() {
        Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
        core.Initialize(null, new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        try {
            CameraComponent camera = new CameraComponent();
            EditorEntity handle = new EditorEntity();
            EditorGizmoHoverService.SetHoveredHandle(camera, handle);
            EditorGizmoDragService.BeginDrag(camera, handle);
            EditorViewportToolService.SetToolMode(camera, EditorViewportToolMode.Rotate);
            TransformGizmoSnapSettingsService.SetSnapValue(camera, EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap1, 99d);

            EditorGizmoHoverService.Reset();
            EditorGizmoDragService.Reset();
            EditorViewportToolService.Reset();
            TransformGizmoSnapSettingsService.ResetDefaults();

            Assert.Null(EditorGizmoHoverService.HoveredHandleEntity);
            Assert.False(EditorGizmoDragService.IsDragging(camera));
            Assert.Equal(EditorViewportToolMode.Translate, EditorViewportToolService.GetToolMode(camera));
            Assert.Equal(5d, TransformGizmoSnapSettingsService.GetSnapValue(camera, EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap1));
            handle.Dispose();
        } finally {
            core.Dispose();
        }
    }
}
