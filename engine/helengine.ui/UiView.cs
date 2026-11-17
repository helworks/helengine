using helengine.ui.format;
namespace helengine.ui {
    /// <summary>Instantiates a portable template using real engine entities and components; owns only its subtree and callback receivers.</summary>
    public sealed class UiView : IDisposable {
        /// <summary>Attached root disposed explicitly before callback receivers; external parents must outlive the view.</summary>
        Entity RootValue;
        /// <summary>Owned callback receiver array, one slot per possible button node.</summary>
        [NativeOwnedMember] UiAction[] Actions;
        /// <summary>Owned table of borrowed entity aliases; the hierarchy owns their storage.</summary>
        [NativeOwnedMember] Entity[] Entities;
        /// <summary>Owned names used because runtime entities carry no authoring name.</summary>
        [NativeOwnedMember] string[] Names;
        /// <summary>Owned local id lookup, independent of node array indexes.</summary>
        [NativeOwnedMember] int[] Ids;
        /// <summary>Resolved metadata exposed for a later platform accessibility bridge.</summary>
        [NativeOwnedMember] string[] Labels;
        /// <summary>Number of registered nodes, including nodes awaiting components.</summary>
        int Count;
        /// <summary>Next callback receiver slot.</summary>
        int ActionCount;
        /// <summary>Allocates bookkeeping before creating any engine-owned hierarchy.</summary>
        UiView(int count) { Actions = new UiAction[count]; Entities = new Entity[count]; Names = new string[count]; Ids = new int[count]; Labels = new string[count]; }
        /// <summary>Returns the attached root as a borrowed placement target.</summary>
        [NativeBorrowedReturn] public Entity Root { get { return RootValue; } }
        /// <summary>Validates the complete document before allocation; bindings and parent must outlive the returned view.</summary>
        [NativeOwnedReturn] public static UiView Create(Entity parent, UiDocument document, string template, IUiBindings bindings, float scale) {
            if (parent == null || bindings == null || !float.IsFinite(scale) || scale <= 0 || scale > 64) { throw new ArgumentException("UI view requires parent, bindings and drawable scale in (0,64]."); }
            UiValidation.Measure(document); UiTemplate selected = null;
            for (int i = 0; i < document.Templates.Length; i++) { if (document.Templates[i].Name == template) { selected = document.Templates[i]; break; } }
            if (selected == null) { throw new FormatException("Unknown UI template: " + template); }
            UiView view = new UiView(selected.Nodes.Length);
            try {
                view.RootValue = Child(parent, template);
                for (int i = 0; i < selected.Nodes.Length; i++) {
                    UiNode node = selected.Nodes[i];
                    UiValues.Bounded(node.X, -32768); UiValues.Bounded(node.Y, -32768); UiValues.Bounded(node.Z, -32768); UiValues.Bounded(node.Width); UiValues.Bounded(node.Height);
                    Entity entity = Child(view.Parent(node.ParentId), node.Name);
                    view.Entities[i] = entity; view.Names[i] = node.Name; view.Ids[i] = node.Id; view.Labels[i] = ""; view.Count++;
                    entity.LocalPosition = new float3(UiValues.Pixels(node.X, scale), UiValues.Pixels(node.Y, scale), node.Z); entity.Enabled = node.Visible;
                    for (int j = 0; j < node.Components.Length; j++) { view.Add(entity, node, node.Components[j], bindings, scale, i); }
                }
                view.RootValue.InitializeHierarchy(); return view;
            } catch { NativeOwnership.DisposeAndRelease(ref view); throw; }
        }
        /// <summary>Creates a fully initialized hierarchy-owned child through the existing factory.</summary>
        [NativeBorrowedReturn] static Entity Child(Entity parent, string name) { if (parent.Children == null) { parent.InitChildren(); } Entity child = parent.OwnerCore.EntityFactory.CreateChild(parent, name); child.LayerMask = parent.LayerMask; child.InitChildren(); child.InitComponents(); return child; }
        /// <summary>Resolves validated parent ids without imposing contiguous numbering.</summary>
        [NativeBorrowedReturn] Entity Parent(int id) { if (id == -1) { return RootValue; } for (int i = 0; i < Count; i++) { if (Ids[i] == id) { return Entities[i]; } } throw new FormatException("Unknown UI parent id."); }
        /// <summary>Finds a node's stable name-table entry and rejects access after disposal.</summary>
        int Index(string name) { for (int i = 0; i < Count; i++) { if (Names[i] == name) { return i; } } throw new InvalidOperationException("Unknown UI node: " + name); }
        /// <summary>Returns an entity borrowed from this view's hierarchy.</summary>
        [NativeBorrowedReturn] public Entity Get(string name) { return Entities[Index(name)]; }
        /// <summary>Returns metadata only; this module does not claim an OS accessibility implementation.</summary>
        public string GetAccessibleLabel(string name) { return Labels[Index(name)]; }
        /// <summary>Maps the supported binary component subset to existing engine APIs.</summary>
        void Add(Entity entity, UiNode node, UiComponent spec, IUiBindings bindings, float scale, int index) {
            int2 size = new int2(UiValues.Pixels(node.Width, scale), UiValues.Pixels(node.Height, scale));
            switch (spec.Kind) {
                case UiComponentKind.Rectangle:
                    entity.AddComponent(new RoundedRectComponent { Size = size, FillColor = UiValues.Color(spec, UiPropertyId.Fill, bindings), BorderColor = UiValues.Color(spec, UiPropertyId.Border, bindings), BorderThickness = UiValues.Bounded(UiValues.Number(spec, UiPropertyId.BorderWidth, 0)) * scale, Radius = UiValues.Bounded(UiValues.Number(spec, UiPropertyId.Radius, 0)) * scale, Corners = (RoundedRectCorners)UiValues.Integer(spec, UiPropertyId.Corners, 15) }); break;
                case UiComponentKind.Text:
                    entity.AddComponent(new TextComponent { Size = size, Text = UiValues.Text(spec, UiPropertyId.Text, bindings), Color = UiValues.Color(spec, UiPropertyId.TextColor, bindings), FontScale = scale, SystemTextStyle = new SystemTextStyle(SystemTextRole.Body, UiValues.Weight(spec), false, UiValues.Number(spec, UiPropertyId.FontSize, 17)), Alignment = UiValues.Alignment(spec), WrapText = UiValues.Boolean(spec, UiPropertyId.Wrap), ClipSystemTextToSize = true, NativeFontPixels = TextNativePixelMode.Disabled }); break;
                case UiComponentKind.Clip: entity.AddComponent(new ClipRectComponent { Size = size }); break;
                case UiComponentKind.Image: AddImage(entity, node, spec, bindings, scale, size); break;
                case UiComponentKind.Button: AddButton(entity, spec, bindings, scale, size, index); break;
                default: throw new FormatException("Unsupported UI component.");
            }
        }
        /// <summary>Applies contain geometry in logical points and leaves the texture with its application owner.</summary>
        void AddImage(Entity entity, UiNode node, UiComponent spec, IUiBindings bindings, float scale, int2 size) {
            RuntimeTexture texture = bindings.GetTexture(UiValues.Find(spec, UiPropertyId.Image).Text); Entity target = entity;
            if (UiValues.Integer(spec, UiPropertyId.ImageFit, 0) == 1) {
                target = Child(entity, node.Name + ".Image");
                if (texture != null) {
                    UiValues.Bounded(texture.Width, 1); UiValues.Bounded(texture.Height, 1);
                    double fit = Math.Min((double)node.Width / texture.Width, (double)node.Height / texture.Height);
                    target.LocalPosition = new float3(UiValues.Pixels((float)((node.Width - texture.Width * fit) / 2), scale), UiValues.Pixels((float)((node.Height - texture.Height * fit) / 2), scale), 0);
                    size = new int2(UiValues.Pixels((float)(texture.Width * fit), scale), UiValues.Pixels((float)(texture.Height * fit), scale));
                }
            }
            target.AddComponent(new SpriteComponent { Size = size, Texture = texture, SourceRect = new float4(0, 0, 1, 1) });
        }
        /// <summary>Creates a native button, rejecting wire options the current engine button cannot express.</summary>
        void AddButton(Entity entity, UiComponent spec, IUiBindings bindings, float scale, int2 size, int index) {
            int corners = UiValues.Integer(spec, UiPropertyId.Corners, 15);
            if ((corners != 0 && corners != 3 && corners != 15) || UiValues.Integer(spec, UiPropertyId.Alignment, 1) != 1 || UiValues.Boolean(spec, UiPropertyId.Wrap)) { throw new NotSupportedException("Native UI buttons support centered unwrapped text and all/top/no rounded corners."); }
            string key = UiValues.Find(spec, UiPropertyId.Action).Text;
            if (!bindings.HasAction(key)) { throw new FormatException("Unknown UI action: " + key); }
            Labels[index] = UiValues.Text(spec, UiPropertyId.AccessibleLabel, bindings);
            // Resolve every fallible binding before allocating a detached engine component.
            string label = UiValues.Text(spec, UiPropertyId.Text, bindings);
            byte4 fill = UiValues.Color(spec, UiPropertyId.Fill, bindings), hover = UiValues.Color(spec, UiPropertyId.Hover, bindings), pressed = UiValues.Color(spec, UiPropertyId.Pressed, bindings), focused = UiValues.Color(spec, UiPropertyId.Focused, bindings), border = UiValues.Color(spec, UiPropertyId.Border, bindings), focusBorder = UiValues.Color(spec, UiPropertyId.FocusBorder, bindings), textColor = UiValues.Color(spec, UiPropertyId.TextColor, bindings);
            float radius = UiValues.Bounded(UiValues.Number(spec, UiPropertyId.Radius, 0)) * scale, thickness = UiValues.Bounded(UiValues.Number(spec, UiPropertyId.BorderWidth, 0)) * scale;
            Actions[ActionCount++] = new UiAction(bindings, key); UiAction action = Actions[ActionCount - 1];
            ButtonComponent button = new ButtonComponent(label, size, null, action.Invoke, thickness) { FontScale = scale, SystemTextStyle = new SystemTextStyle(SystemTextRole.Body, UiValues.Weight(spec), false, UiValues.Number(spec, UiPropertyId.FontSize, 17)) };
            if (corners == 0) { button.UseSquareCorners(); } else if (corners == 3) { button.UseTopCorners(); }
            button.SetCornerRadius(radius); button.SetVisualPalette(fill, hover, pressed, focused, border, focusBorder); button.SetTextColor(textColor); entity.AddComponent(button);
        }
        /// <summary>Removes the complete hierarchy before releasing callback receivers; borrowed images/providers are untouched.</summary>
        public void Dispose() { NativeOwnership.Release(ref Entities); NativeOwnership.Release(ref Names); NativeOwnership.Release(ref Ids); NativeOwnership.Release(ref Labels); Count = 0; if (RootValue != null) { NativeOwnership.DisposeAndDelete(RootValue); RootValue = null; } NativeOwnership.DeleteItemsAndRelease(ref Actions); ActionCount = 0; }
    }
}
