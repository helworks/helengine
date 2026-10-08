global using helengine;
using DemoDisc.menu;
using System.Reflection;
using System.Text.Json;

/// <summary>Runs real engine heading layout and menu binding regressions without a renderer or emulator.</summary>
internal static class TitleFitTests {
    /// <summary>Collects successful checks for the fixed validation receipt.</summary>
    static readonly List<string> Checks = new List<string>();

    /// <summary>Runs the minimum title fitting, style, lifecycle and allocation checks.</summary>
    public static void Main() {
        Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory),
            SceneCatalog = new RuntimeSceneCatalog(new[] { new RuntimeSceneCatalogEntry("menu", "cooked/menu.hasset") }) });
        core.Initialize(null, null, null, new PlatformInfo("test", "test"));
        FontAsset font = CreateFont();
        Entity root = Node(core);
        TextComponent hel = Heading(core, root, font, "HELENGINE");
        TextComponent demo = Heading(core, root, font, "DEMO DISC");
        TextComponent other = Heading(core, root, font, "button");
        Check(ReferenceEquals(DemoDiscTitleLayout.Find(root, "HELENGINE"), hel)
            && ReferenceEquals(DemoDiscTitleLayout.Find(root, "DEMO DISC"), demo)
            && DemoDiscTitleLayout.Find(root, "helengine") == null, "Binding finds exact case-sensitive headings and borrows their identities");
        Check(Math.Abs(TextLayoutAlignmentUtils.MeasureVisibleLineWidth("HELENGINE", font, 1d, 512) - 158.8125d) < 0.0001d,
            "Real engine measurement preserves uppercase glyph metrics despite a distinct lowercase e");

        ViewportLayoutSnapshot helSnapshot = new ViewportLayoutSnapshot(hel.Parent, false);
        ViewportLayoutSnapshot demoSnapshot = new ViewportLayoutSnapshot(demo.Parent, false);
        Apply(helSnapshot, demoSnapshot, hel, demo, 640, 480);
        Check(Bounded(hel, 140) && Bounded(demo, 140), "Both 140-pixel headings include outline and one raster pixel inside their right boxes");
        Check(Math.Abs(PaintedRight(hel, 618d, false) - 617d) < 0.0001d
            && Math.Abs(PaintedRight(demo, 618d, false) - 617d) < 0.0001d,
            "Different heading widths share the same painted right edge one pixel inside the overlay anchor");
        Check(Math.Abs(PaintedRight(hel, 618d, true) - 617d) <= 0.5001d
            && Math.Abs(PaintedRight(demo, 618d, true) - 617d) <= 0.5001d,
            "Native world-origin rounding stays within half a raster pixel for both painted edges");
        Check(hel.FontScale == demo.FontScale && hel.OutlineScale == demo.OutlineScale
            && hel.Parent.LocalPosition.X == demo.Parent.LocalPosition.X
            && Math.Abs(PaintedRight(hel, 618d, true) - PaintedRight(demo, 618d, true)) < 0.0001d,
            "One shared reducing ratio gives both headings identical effects, snapped origins and painted right edges");
        float scale480 = hel.FontScale;
        Apply(helSnapshot, demoSnapshot, hel, demo, 320, 240);
        Check(Bounded(hel, 70) && Bounded(demo, 70), "Both 70-pixel headings fit with effects and one raster pixel");
        Check(Math.Abs(PaintedRight(hel, 309d, false) - 308d) < 0.0001d
            && Math.Abs(PaintedRight(demo, 309d, false) - 308d) < 0.0001d,
            "Viewport resize restores the same right margin despite the two original local X offsets");
        Check(Math.Abs(hel.OutlineScale / hel.FontScale - 1f) < 0.0001f
            && Math.Abs(hel.ShadowOffset.X / hel.FontScale) < 0.0001f, "Authored font, outline and shadow proportions are preserved");
        float oldScale = hel.FontScale;
        for (int index = 0; index < 1000; index++) {
            DemoDiscTitleLayout.FitPair(hel, demo);
        }
        Check(hel.FontScale == oldScale, "Repeated updates are exactly idempotent without float drift");
        Apply(helSnapshot, demoSnapshot, hel, demo, 640, 480);
        Check(hel.FontScale == scale480, "Viewport snapshot resize round trip restores the identical fitted scale");
        Check(other.FontScale == 2f && other.OutlineScale == 2f && other.Size.X == 280,
            "Unrelated button text and authored geometry remain unchanged");

        hel.FontScale = 2f;
        hel.OutlineScale = 2f;
        hel.ShadowOffset = new float2(6f, 8f);
        hel.Size = new int2(140, 28);
        DemoDiscTitleLayout.Fit(hel);
        Check(Bounded(hel, 140) && Math.Abs(hel.ShadowOffset.X / hel.FontScale - 3f) < 0.0001f
            && Math.Abs(hel.ShadowOffset.Y / hel.FontScale - 4f) < 0.0001f,
            "A larger positive shadow determines the right effect and scales both shadow axes together");
        Check(Math.Abs(PaintedRight(hel, 618d, false) - 617d) < 0.0001d,
            "A positive shadow remains inside the same painted right boundary after fitting");
        hel.FontScale = 2f;
        hel.OutlineScale = 2f;
        hel.ShadowOffset = new float2(-20f, 4f);
        DemoDiscTitleLayout.Fit(hel);
        Check(Bounded(hel, 140) && Math.Abs(hel.FontScale - 139f / 159.8125f) < 0.0001f,
            "Negative horizontal shadow does not incorrectly consume right-side width");
        TextComponent small = Heading(core, root, font, "H");
        DemoDiscTitleLayout.Fit(small);
        Check(small.FontScale == 2f && small.OutlineScale == 2f,
            "Already fitting text preserves every style value");
        Check(small.Alignment == TextAlignment.Right && Math.Abs(PaintedRight(small, 618d, false) - 617d) < 0.0001d,
            "Short text also aligns by painted bounds without growing its approved font scale");

        Entity generated = Node(core);
        root.AddChild(generated);
        Entity panel = Node(core);
        generated.AddChild(panel);
        panel.AddComponent(new MenuPanelComponent { PanelId = "main" });
        Entity viewport = Node(core);
        panel.AddChild(viewport);
        viewport.AddComponent(new ClipRectComponent { Size = new int2(140, 100) });
        Entity rows = Node(core);
        viewport.AddChild(rows);
        rows.AddComponent(new ScrollComponent { VisibleItemCount = 1 });
        Entity row = Node(core);
        rows.AddChild(row);
        row.AddComponent(new MenuItemComponent { PanelId = "main", ItemId = "test", ActionKind = MenuActionKind.None,
            IdleFillColor = new byte4(1, 2, 3, 255), IdleBorderColor = new byte4(1, 2, 3, 255),
            SelectedFillColor = new byte4(1, 2, 3, 255), SelectedBorderColor = new byte4(1, 2, 3, 255) });
        row.AddComponent(new RoundedRectComponent { Size = new int2(140, 20) });
        MenuComponent menu = new MenuComponent { InitialPanelId = "main" };
        root.AddComponent(menu);
        typeof(MenuComponent).GetMethod("TryInitialize", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu, null);
        Check(ReferenceEquals(Cached(menu, "FittedHelengineTitle"), hel)
            && ReferenceEquals(Cached(menu, "FittedDemoDiscTitle"), demo), "Menu binding caches the two existing components once");
        hel.FontScale = 2f;
        hel.OutlineScale = 2f;
        hel.ShadowOffset = new float2();
        demo.FontScale = 2f;
        demo.OutlineScale = 2f;
        demo.Size = new int2(140, 28);
        menu.Update();
        Check(Bounded(hel, 140) && Bounded(demo, 140), "The actual MenuComponent.Update hook fits both cached titles");
        long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1000; index++) {
            DemoDiscTitleLayout.FitPair(hel, demo);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
        Check(allocated == 0, "Both per-frame heading fits allocate zero managed bytes");
        menu.ComponentRemoved(root);
        Check(Cached(menu, "FittedHelengineTitle") == null && Cached(menu, "FittedDemoDiscTitle") == null
            && ReferenceEquals(hel.Parent.Components[0], hel) && ReferenceEquals(demo.Parent.Components[0], demo),
            "Unbinding clears borrowed caches without removing or deleting their entity-owned text components");
        typeof(MenuComponent).GetField("FittedHelengineTitle", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(menu, hel);
        menu.Dispose();
        Check(Cached(menu, "FittedHelengineTitle") == null && Cached(menu, "FittedDemoDiscTitle") == null
            && ReferenceEquals(hel.Parent.Components[0], hel), "Disposal also clears borrowed caches without taking component ownership");
        File.WriteAllText("C:/dev/helworks/builds/builder-coordination/menu-20261007/menu-title-fit/test-receipt.json",
            JsonSerializer.Serialize(new { passed = true, checks = Checks.Count, results = Checks, perUpdateAllocatedBytes = allocated },
                new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS: {Checks.Count} common title fitting checks; per-update allocation={allocated}");
    }

    /// <summary>Constructs the actual uppercase body-font metrics needed by both authored titles.</summary>
    static FontAsset CreateFont() {
        Dictionary<char, FontChar> glyphs = new Dictionary<char, FontChar>();
        char[] keys = "HELANGIDMOSC".ToCharArray();
        float[] widths = { 19f, 16f, 16f, 19f, 18f, 21f, 6f, 19f, 25f, 22f, 15f, 19f };
        float[] advances = { 20.9375f, 17.140625f, 16.203125f, 20f, 20.421875f, 21.640625f,
            8.90625f, 19.625f, 22.703125f, 21.5625f, 15.390625f, 19.140625f };
        for (int index = 0; index < keys.Length; index++) {
            glyphs[keys[index]] = new FontChar(new float4(0f, 0f, widths[index] / 512f, 25f / 256f), 8f, advances[index], 0f, 0f);
        }
        glyphs['e'] = new FontChar(new float4(0f, 0f, 18f / 512f, 19f / 256f), 14f, 17.453125f, 0f, 0f);
        return new FontAsset(new FontInfo("Trebuchet MS", 38, 9.640625f), null, glyphs, 37.15625f, 512, 256);
    }

    /// <summary>Creates an entity with initialized engine-owned child and component containers.</summary>
    static Entity Node(Core core) {
        Entity entity = new Entity(core);
        entity.InitComponents();
        entity.InitChildren();
        return entity;
    }

    /// <summary>Attaches one authored-size text to a child entity, borrowing the supplied font.</summary>
    static TextComponent Heading(Core core, Entity parent, FontAsset font, string text) {
        Entity entity = Node(core);
        parent.AddChild(entity);
        TextComponent component = new TextComponent { Text = text, Font = font, FontScale = 2f, OutlineScale = 2f,
            ShadowOffset = new float2(), Size = new int2(280, 28) };
        entity.AddComponent(component);
        return component;
    }

    /// <summary>Runs the real viewport snapshot before refitting the two headings.</summary>
    static void Apply(ViewportLayoutSnapshot helSnapshot, ViewportLayoutSnapshot demoSnapshot,
        TextComponent hel, TextComponent demo, int width, int height) {
        AnchorSpace space = new AnchorSpace(new int2(width, height), new float2());
        helSnapshot.Apply(space, new float2(), 1280, 720, true);
        demoSnapshot.Apply(space, new float2(), 1280, 720, true);
        DemoDiscTitleLayout.FitPair(hel, demo);
    }

    /// <summary>Measures the engine's actual visible glyphs, right effects and raster margin against a text box.</summary>
    static bool Bounded(TextComponent text, int boxWidth) {
        double width = TextLayoutAlignmentUtils.MeasureVisibleLineWidth(text.Text, text.Font, text.FontScale, text.Font.AtlasWidth);
        double rightEffect = Math.Max((double)text.OutlineScale, Math.Max(0d, (double)text.ShadowOffset.X));
        return width + rightEffect + 1d <= boxWidth + 0.0001d;
    }

    /// <summary>Combines the renderer's visible-width right alignment with the actual heading position and right effects.</summary>
    static double PaintedRight(TextComponent text, double anchorWorldX, bool roundOrigin) {
        double visibleWidth = TextLayoutAlignmentUtils.MeasureVisibleLineWidth(text.Text, text.Font, text.FontScale, text.Font.AtlasWidth);
        double origin = anchorWorldX + text.Parent.LocalPosition.X;
        if (roundOrigin) {
            origin = Math.Round(origin);
        }
        double offset = TextLayoutAlignmentUtils.ResolveHorizontalOffset(text.Alignment, text.Size.X, visibleWidth);
        double effect = Math.Max((double)text.OutlineScale, Math.Max(0d, (double)text.ShadowOffset.X));
        return origin + offset + visibleWidth + effect;
    }

    /// <summary>Reads a private borrowed heading cache for lifecycle identity assertions.</summary>
    static object Cached(MenuComponent menu, string name) {
        return typeof(MenuComponent).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
    }

    /// <summary>Fails one meaningful assertion or records its successful result.</summary>
    static void Check(bool value, string label) {
        if (!value) {
            throw new InvalidOperationException(label);
        }
        Checks.Add(label);
    }
}
