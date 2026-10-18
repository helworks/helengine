namespace DemoDisc.MenuContractTests {
    /// <summary>Builds a headless baked hierarchy from the original complete menu metadata and the actual replacement runtime sources.</summary>
    internal sealed class MenuContractFixture : IDisposable {
        /// <summary>The fixed row spacing makes accidental availability-driven reflow observable.</summary>
        public const float RowStep = 30f;

        /// <summary>Creates all authored panels before invoking the production readiness callback.</summary>
        /// <param name="catalogKind">Null, empty, menu-and-cube, or every declared scene.</param>
        /// <param name="prepare">Whether to prepare immediately or allow a title fixture to add its hierarchy first.</param>
        public MenuContractFixture(string catalogKind, bool prepare = true) {
            Definition = new DemoDiscMenuDefinitionProvider().CreateMenuDefinition();
            Core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory),
                SceneCatalog = CreateCatalog(catalogKind, Definition)
            });
            Core.Initialize(null, null, null, new PlatformInfo("menu-contract", "headless"));
            Root = CreateNode();
            Entity generated = CreateNode();
            Root.AddChild(generated);
            foreach (MenuPanelDefinition panel in Definition.Panels) {
                AddPanel(generated, panel);
            }
            Menu = new MenuComponent { InitialPanelId = Definition.InitialPanelId };
            Root.AddComponent(Menu);
            if (prepare) {
                Menu.ScenePrepared(Root);
            }
        }

        /// <summary>Gets the isolated engine that supplies the catalog and transition service.</summary>
        public Core Core { get; }

        /// <summary>Gets the original provider definition used as the expected baked contract.</summary>
        public MenuDefinition Definition { get; }

        /// <summary>Gets the owning hierarchy; runtime panel records borrow its children.</summary>
        public Entity Root { get; }

        /// <summary>Gets the actual source-compiled component under test.</summary>
        public MenuComponent Menu { get; }

        /// <summary>Returns the production bound panel table without replacing its records.</summary>
        public Dictionary<string, MenuPanelRuntime> Panels =>
            (Dictionary<string, MenuPanelRuntime>)typeof(MenuComponent).GetField("PanelsById", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Menu);

        /// <summary>Creates an entity with the same initialized containers expected by baked menu binding.</summary>
        /// <returns>An entity owned by this fixture's engine.</returns>
        public Entity CreateNode() {
            Entity entity = new Entity(Core);
            entity.InitComponents();
            entity.InitChildren();
            return entity;
        }

        /// <summary>Invokes the private navigation seam without synthesizing host keyboard or controller input.</summary>
        /// <param name="method">The existing runtime method being exercised.</param>
        /// <param name="arguments">Arguments forwarded unchanged to that method.</param>
        public void Invoke(string method, params object[] arguments) {
            typeof(MenuComponent).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Menu, arguments);
        }

        /// <summary>Disposes hierarchy owners before the engine; repeated calls retain their existing idempotence.</summary>
        public void Dispose() {
            Root.Dispose();
            Core.Dispose();
        }

        /// <summary>Materializes every authored item in its original order with explicit clipping and scroll ownership.</summary>
        /// <param name="generated">Parent subtree collected by the production binder.</param>
        /// <param name="definition">Original logical panel and action metadata.</param>
        void AddPanel(Entity generated, MenuPanelDefinition definition) {
            Entity panel = CreateNode();
            generated.AddChild(panel);
            panel.AddComponent(new MenuPanelComponent { PanelId = definition.PanelId });
            Entity viewport = CreateNode();
            panel.AddChild(viewport);
            viewport.AddComponent(new ClipRectComponent { Size = new int2(200, 120) });
            Entity items = CreateNode();
            viewport.AddChild(items);
            items.AddComponent(new ScrollComponent { ShowScrollBar = false, VisibleItemCount = definition.VisibleItemCount });
            for (int index = 0; index < definition.Items.Length; index++) {
                MenuItemDefinition item = definition.Items[index];
                Entity row = CreateNode();
                row.LocalPosition = new float3(0f, index * RowStep, 0f);
                items.AddChild(row);
                row.AddComponent(new MenuItemComponent {
                    PanelId = definition.PanelId,
                    ItemId = item.ItemId,
                    ActionKind = item.Action.Kind,
                    TargetId = item.Action.TargetId,
                    IdleFillColor = Definition.SurfaceColor,
                    IdleBorderColor = Definition.SurfaceBorderColor,
                    SelectedFillColor = Definition.AccentColor,
                    SelectedBorderColor = Definition.AccentSecondaryColor
                });
                row.AddComponent(new RoundedRectComponent { Size = new int2(100, 20) });
                Entity label = CreateNode();
                row.AddChild(label);
                label.AddComponent(new TextComponent { Text = item.Label, Color = Definition.TextColor });
            }
        }

        /// <summary>Constructs the four real catalog configurations without discovering or opening scene files.</summary>
        /// <param name="kind">The explicit catalog case selected by the regression.</param>
        /// <param name="definition">Complete original menu metadata supplying the full-catalog scene ids.</param>
        /// <returns>The requested catalog, including a null catalog for authoring hosts.</returns>
        static RuntimeSceneCatalog CreateCatalog(string kind, MenuDefinition definition) {
            if (kind == "null") {
                return null;
            }
            List<RuntimeSceneCatalogEntry> entries = new List<RuntimeSceneCatalogEntry>();
            if (kind == "cube-only" || kind == "full") {
                entries.Add(new RuntimeSceneCatalogEntry("Scenes/DemoDiscMainMenu.helen", "cooked/menu.helen"));
                entries.Add(new RuntimeSceneCatalogEntry("cube_test", "cooked/cube.helen"));
            } else if (kind != "empty") {
                throw new ArgumentException("Unknown explicit catalog case.", nameof(kind));
            }
            if (kind == "full") {
                foreach (MenuPanelDefinition panel in definition.Panels) {
                    foreach (MenuItemDefinition item in panel.Items) {
                        if (item.Action.Kind == MenuActionKind.LoadScene
                            && !entries.Any(entry => entry.SceneId == item.Action.TargetId)) {
                            entries.Add(new RuntimeSceneCatalogEntry(item.Action.TargetId, "cooked/" + item.Action.TargetId + ".helen"));
                        }
                    }
                }
            }
            return new RuntimeSceneCatalog(entries.ToArray());
        }
    }
}
