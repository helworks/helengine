using System.Runtime.CompilerServices;
using Xunit;
using helengine.ui;
using helengine.editor.tests.testing;

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
            new[] { "second-detacher", "first-detacher", "publisher", "first-detacher" },
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
