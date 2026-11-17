using helengine;
namespace helengine.core.tests.components {
    /// <summary>Verifies complete native composition import and density-aware existing controls.</summary>
    public sealed class NativeTextEntryTests {
        /// <summary>Native import must preserve marked UTF-16 text atomically rather than typing independent key characters.</summary>
        [Fact]
        public void CompleteCompositionPreservesMarkedUnicode() {
            var method = typeof(TextBoxEditState).GetMethod("ApplyNativeSnapshot");
            Assert.NotNull(method);
            TextBoxEditState state = new TextBoxEditState("old");
            method.Invoke(state, new object[] { "A\U0001F469\u200d\U0001F469\u200d\U0001F467\u200d\U0001F466e\u0301", 12, 2, 12, 2 });
            Assert.Equal("A\U0001F469\u200d\U0001F469\u200d\U0001F467\u200d\U0001F466e\u0301", state.Text);
            Assert.Equal(12, state.SelectionStart);
            Assert.Equal(14, state.SelectionEnd);
        }
        /// <summary>Existing TextBox and Button must expose an explicit validated font scale for physical-pixel layouts.</summary>
        [Fact]
        public void ExistingControlsExposeFontScale() {
            Assert.NotNull(typeof(TextBoxComponent).GetProperty("FontScale"));
            Assert.NotNull(typeof(ButtonComponent).GetProperty("FontScale"));
        }
        /// <summary>Invalid payloads fail atomically, while selection endpoints stay on UTF-16 scalar boundaries.</summary>
        [Fact]
        public void InvalidSnapshotsAreAtomicAndCompositionCommitsExactly() {
            TextBoxEditState state = new TextBoxEditState("ok");
            Assert.Throws<ArgumentOutOfRangeException>(() => state.ApplyNativeSnapshot(new string('x', 16385), 0, 0, -1, 0));
            Assert.Equal("ok", state.Text);
            state.ApplyNativeSnapshot("A\U0001F31EB", 2, 0, -1, 0);
            Assert.Equal(1, state.CursorPosition);
            Assert.Throws<ArgumentException>(() => state.ApplyNativeSnapshot("\ud800", 0, 0, -1, 0));
            Assert.Equal("A\U0001F31EB", state.Text);
            state.ApplyNativeSnapshot("\u306b", 1, 0, 0, 1);
            Assert.Equal(0, state.MarkedStart);
            state.ApplyNativeSnapshot("\u65e5\u672c", 2, 0, -1, 0);
            Assert.Equal(-1, state.MarkedStart);
            Assert.Equal("\u65e5\u672c", state.Text);
            state.SetSelection(0, 2); state.InsertText("\U0001F31E");
            Assert.Equal("\U0001F31E", state.Text);
        }
        /// <summary>Complete snapshots update only their owner; equal external text preserves composition, focus changes dispose stale sessions.</summary>
        [Fact]
        public void FocusSwitchExternalTextAndDisposalOwnSessions() {
            NativeTextEntryTestBackend backend = new NativeTextEntryTestBackend();
            using helengine.core.tests.managers.rendering.Batch2DTestRenderManager renderer = new helengine.core.tests.managers.rendering.Batch2DTestRenderManager();
            using Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory), TextEntryBackend = backend });
            core.Initialize(null, renderer, null, new PlatformInfo("test", "test"));
            TextBoxComponent first = Attach(core); TextBoxComponent second = Attach(core);
            first.Text = "initial"; first.IsFocused = true;
            NativeTextEntryTestSession old = backend.Session;
            first.IsFocused = true;
            Assert.Same(old, backend.Session);
            Assert.False(old.Disposed);
            old.Publish("\u306b", 1, 0, 0, 1); first.Update();
            Assert.Equal("\u306b", first.Text);
            first.Text = first.Text;
            Assert.Equal(0, old.ReplacementCount); Assert.Equal(0, old.MarkedStart);
            first.Text = "external\U0001F31E";
            Assert.Equal(first.Text, old.Text); Assert.Equal(-1, old.MarkedStart);
            second.IsFocused = true;
            Assert.False(first.IsFocused); Assert.True(old.Disposed);
            old.Publish("stale", 5, 0, -1, 0); first.Update();
            Assert.Equal("external\U0001F31E", first.Text);
            NativeTextEntryTestSession active = backend.Session;
            active.Publish("commit", 6, 0, -1, 0); active.Submitted = true; second.Update();
            Assert.Equal("commit", second.Text); Assert.False(second.IsFocused); Assert.True(active.Disposed);
            second.IsFocused = true; active = backend.Session; second.ComponentRemoved(second.Parent);
            Assert.True(active.Disposed);
            first.IsFocused = true; active = backend.Session; first.Dispose(); Assert.True(active.Disposed);
            TextBoxComponent detached = new TextBoxComponent(new int2(100, 40), null); detached.IsFocused = true; Assert.False(detached.IsFocused);
        }
        /// <summary>Blur consumes the last complete host edit even when no update has run, and interruption does not submit canceled input.</summary>
        [Fact]
        public void BlurDrainsPendingTextAndInterruptionDoesNotSubmit() {
            NativeTextEntryTestBackend backend = new NativeTextEntryTestBackend();
            using helengine.core.tests.managers.rendering.Batch2DTestRenderManager renderer = new helengine.core.tests.managers.rendering.Batch2DTestRenderManager();
            using Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory), TextEntryBackend = backend });
            core.Initialize(null, renderer, null, new PlatformInfo("test", "test"));
            TextBoxComponent box = Attach(core); int submissions = 0; box.Submitted += owner => submissions++;
            box.IsFocused = true; backend.Session.Publish("last edit\U0001F31E", 11, 0, -1, 0); box.IsFocused = false;
            Assert.Equal("last edit\U0001F31E", box.Text); Assert.Equal(1, submissions);
            box.IsFocused = true; backend.CancelActive(); box.Update();
            Assert.False(box.IsFocused); Assert.True(backend.Session.Disposed); Assert.Equal(1, submissions);
        }
        /// <summary>Legacy editing remains usable; invalid explicit density cannot poison existing controls.</summary>
        [Fact]
        public void LegacySelectionAndScaleValidationRemainUsable() {
            TextBoxEditState state = new TextBoxEditState("abcd"); state.SetSelection(1, 3); state.InsertText("X"); state.Backspace(); state.Delete();
            Assert.Equal("a", state.Text);
            TextBoxComponent box = new TextBoxComponent(new int2(100, 40), null); Assert.Equal(1f, box.FontScale); box.FontScale = 2f; Assert.Equal(2f, box.FontScale);
            Assert.Throws<ArgumentOutOfRangeException>(() => box.FontScale = float.NaN);
            ButtonComponent button = new ButtonComponent("label", new int2(100, 40), null); Assert.Equal(1f, button.FontScale); button.FontScale = 2f;
            Assert.Throws<ArgumentOutOfRangeException>(() => button.FontScale = 0f);
        }
        /// <summary>The existing text child keeps the complete glyph string while its render request follows the caret with a bounded shifted window.</summary>
        [Fact]
        public void CaretIsSeparateAndScrolledTextUsesShiftedRasterWindow() {
            NativeTextEntryTestBackend input = new NativeTextEntryTestBackend();
            using helengine.core.tests.managers.rendering.Batch2DTestRenderManager renderer = new helengine.core.tests.managers.rendering.Batch2DTestRenderManager();
            using Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory), TextEntryBackend = input });
            core.Initialize(null, renderer, null, new PlatformInfo("test", "test"));
            var shaping = new helengine.core.tests.managers.rendering.SystemTextTestBackend(new helengine.core.tests.managers.rendering.SystemTextTestLayout(renderer.CreateTexture(40, 24)));
            renderer.SystemTextBackend = shaping;
            TextBoxComponent box = Attach(core); box.Size = new int2(30, 40); box.SystemTextStyle = new SystemTextStyle(SystemTextRole.Body); box.FontScale = 2f; box.Text = "AB"; box.IsFocused = true; box.Update();
            TextComponent text = (TextComponent)typeof(TextBoxComponent).GetField("TextComponent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(box);
            Assert.Equal("AB", text.Text);
            Assert.Equal(2f, text.FontScale);
            Assert.True(text.ClipSystemTextToSize);
            Assert.True(shaping.LastWindow.X > 0f);
            float4 measuredWindow = shaping.LastWindow;
            using RenderList2D queue = new RenderList2D(1); queue.Add(text);
            using RenderCommandListBuilder2D builder = new RenderCommandListBuilder2D { SystemTextBackend = shaping }; builder.Build(queue);
            Assert.Equal(measuredWindow, shaping.LastWindow);
            box.IsFocused = false; text.MeasureSystemText(); Assert.Equal(0f, shaping.LastWindow.X);
        }
        /// <summary>A rejected complete replacement preserves portable selection, marked composition, displayed text and native revision.</summary>
        [Theory]
        [InlineData("a\nb")]
        [InlineData("other replacement")]
        public void RejectedExternalReplacementPreservesEntirePreviousSnapshot(string replacement) {
            NativeTextEntryTestBackend input = new NativeTextEntryTestBackend();
            using helengine.core.tests.managers.rendering.Batch2DTestRenderManager renderer = new helengine.core.tests.managers.rendering.Batch2DTestRenderManager();
            using Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory), TextEntryBackend = input });
            core.Initialize(null, renderer, null, new PlatformInfo("test", "test"));
            TextBoxComponent box = Attach(core); box.Text = "AB"; box.IsFocused = true;
            input.Session.Publish("AB", 1, 0, 0, 2); box.Update(); input.Session.RejectReplacement = true;
            int revision = input.Session.Revision;
            TextBoxEditState edit = (TextBoxEditState)ReadField(box, "EditState");
            TextComponent text = (TextComponent)ReadField(box, "TextComponent");
            Assert.Throws<InvalidOperationException>(() => box.Text = replacement);
            Assert.Equal("AB", box.Text); Assert.Equal("AB", text.Text); Assert.Equal("AB", input.Session.Text);
            Assert.Equal(1, edit.SelectionStart); Assert.Equal(1, edit.SelectionEnd); Assert.Equal(0, edit.MarkedStart); Assert.Equal(2, edit.MarkedLength); Assert.Equal(revision, input.Session.Revision);
            box.Update(); Assert.Equal("AB", box.Text); Assert.Equal(0, edit.MarkedStart);
        }
        /// <summary>A line taller than its control rasterizes the inverse centered window and bounds all feedback to the same visible viewport.</summary>
        [Fact]
        public void TallSystemTextCentersItsRasterWindowAndClipsFeedback() {
            NativeTextEntryTestBackend input = new NativeTextEntryTestBackend();
            using helengine.core.tests.managers.rendering.Batch2DTestRenderManager renderer = new helengine.core.tests.managers.rendering.Batch2DTestRenderManager();
            using Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory), TextEntryBackend = input });
            core.Initialize(null, renderer, null, new PlatformInfo("test", "test"));
            NativeTallTextTestBackend shaping = new NativeTallTextTestBackend(renderer.CreateTexture(40, 80)); renderer.SystemTextBackend = shaping;
            TextBoxComponent box = Attach(core); box.Size = new int2(200, 20); box.SystemTextStyle = new SystemTextStyle(SystemTextRole.Body); box.Text = "AB"; box.IsFocused = true; box.Update();
            TextComponent text = (TextComponent)ReadField(box, "TextComponent"); text.MeasureSystemText(); Assert.Equal(30f, shaping.LastWindow.Y); Assert.Equal(20f, shaping.LastWindow.W);
            using RenderList2D queue = new RenderList2D(1); queue.Add(text);
            using RenderCommandListBuilder2D builder = new RenderCommandListBuilder2D { SystemTextBackend = shaping }; RenderCommandList2D commands = builder.Build(queue);
            int quad = commands.GetTexturedQuadPayloadIndex(0); float4 bounds = commands.GetTexturedQuadBounds(quad); Assert.Equal(0f, bounds.Y); Assert.Equal(20f, bounds.W);
            Entity caret = (Entity)ReadField(box, "CaretEntity"); RoundedRectComponent caretSprite = (RoundedRectComponent)ReadField(box, "CaretSprite");
            Assert.Equal(0f, caret.Position.Y); Assert.Equal(20, caretSprite.Size.Y);
            input.Session.Publish("AB", 0, 2, 0, 2); box.Update();
            Entity selection = (Entity)ReadField(box, "SelectionEntity"); RoundedRectComponent selectionSprite = (RoundedRectComponent)ReadField(box, "SelectionSprite"); RoundedRectComponent markedSprite = (RoundedRectComponent)ReadField(box, "MarkedSprite");
            Assert.Equal(0f, selection.Position.Y); Assert.Equal(20, selectionSprite.Size.Y); Assert.Equal(0, markedSprite.Size.Y);
        }
        /// <summary>Bitmap button glyph geometry centers the scaled line at both integer and fractional density.</summary>
        [Theory]
        [InlineData(2f, 10f, 40)]
        [InlineData(1.5f, 15f, 30)]
        public void BitmapButtonCentersScaledLine(float scale, float expectedY, int expectedHeight) {
            using helengine.core.tests.managers.rendering.Batch2DTestRenderManager renderer = new helengine.core.tests.managers.rendering.Batch2DTestRenderManager();
            using Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory) }); core.Initialize(null, renderer, null, new PlatformInfo("test", "test"));
            RuntimeTexture atlas = renderer.CreateTexture(8, 8);
            using FontAsset font = new FontAsset(new FontInfo("scaled button", 20, 3f), atlas, new Dictionary<char, FontChar>(), 20f, 8, 8);
            Entity entity = new Entity(core); entity.Enabled = true; entity.InitComponents(); entity.InitChildren();
            ButtonComponent button = new ButtonComponent("", new int2(100, 60), font) { FontScale = scale }; entity.AddComponent(button);
            TextComponent text = (TextComponent)ReadField(button, "TextComponent"); Entity textEntity = (Entity)ReadField(button, "TextEntity");
            Assert.Equal(expectedY, textEntity.Position.Y); Assert.Equal(expectedHeight, text.Size.Y); Assert.Equal(scale, text.FontScale);
        }
        /// <summary>Final component disposal runs after child entities are gone and must release input without redrawing dead feedback.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EntityDisposalReleasesSessionAfterFeedbackChildrenAreDestroyed(bool focused) {
            NativeTextEntryTestBackend input = new NativeTextEntryTestBackend();
            using helengine.core.tests.managers.rendering.Batch2DTestRenderManager renderer = new helengine.core.tests.managers.rendering.Batch2DTestRenderManager();
            using Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory), TextEntryBackend = input }); core.Initialize(null, renderer, null, new PlatformInfo("test", "test"));
            using FontAsset font = new FontAsset(new FontInfo("disposal", 20, 3f), renderer.CreateTexture(8, 8), new Dictionary<char, FontChar>(), 20f, 8, 8);
            Entity entity = new Entity(core); entity.Enabled = true; entity.InitComponents(); entity.InitChildren();
            TextBoxComponent box = new TextBoxComponent(new int2(100, 40), font); entity.AddComponent(box); box.Text = "text";
            if (focused) { box.IsFocused = true; input.Session.Publish("last", 4, 0, 0, 4); }
            entity.Dispose();
            Assert.True(entity.IsDisposed); Assert.False(box.IsFocused); Assert.NotSame(box, TextBoxComponent.FocusedTextEntry);
            if (focused) { Assert.True(input.Session.Disposed); Assert.True(input.Session.Canceled); }
            box.Dispose();
        }
        /// <summary>Inspects existing private control children without adding production test-only accessors.</summary>
        static object ReadField(object control, string name) { return control.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(control); }
        /// <summary>Creates the real attached control against a headless owning host.</summary>
        static TextBoxComponent Attach(Core core) {
            Entity entity = new Entity(core); entity.Enabled = true; entity.InitComponents(); entity.InitChildren();
            TextBoxComponent box = new TextBoxComponent(new int2(200, 40), null); entity.AddComponent(box); return box;
        }
    }
}
