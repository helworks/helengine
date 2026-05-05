namespace helengine {
    /// <summary>
    /// Stores the serialized runtime metadata required to navigate one baked demo menu item.
    /// </summary>
    public class DemoMenuItemComponent : Component {
        /// <summary>
        /// Current payload version used by scene persistence for baked menu items.
        /// </summary>
        public const byte CurrentVersion = 1;

        /// <summary>
        /// Stable serialized component type id used by baked menu item scene records.
        /// </summary>
        public const string SerializedComponentTypeId = "helengine.DemoMenuItemComponent";

        /// <summary>
        /// Backing field for the owning panel id.
        /// </summary>
        string PanelIdValue;

        /// <summary>
        /// Backing field for the stable item id.
        /// </summary>
        string ItemIdValue;

        /// <summary>
        /// Backing field for the item description rendered when selected.
        /// </summary>
        string DescriptionValue;

        /// <summary>
        /// Backing field for the action target id or scene path.
        /// </summary>
        string TargetIdValue;

        /// <summary>
        /// Initializes a new baked menu item component with empty metadata.
        /// </summary>
        public DemoMenuItemComponent() {
            PanelIdValue = string.Empty;
            ItemIdValue = string.Empty;
            DescriptionValue = string.Empty;
            TargetIdValue = string.Empty;
        }

        /// <summary>
        /// Gets or sets the stable panel id that owns the item.
        /// </summary>
        public string PanelId {
            get { return PanelIdValue; }
            set { PanelIdValue = value ?? string.Empty; }
        }

        /// <summary>
        /// Gets or sets the stable item id used for selection tracking.
        /// </summary>
        public string ItemId {
            get { return ItemIdValue; }
            set { ItemIdValue = value ?? string.Empty; }
        }

        /// <summary>
        /// Gets or sets the supporting description shown when the item is selected.
        /// </summary>
        public string Description {
            get { return DescriptionValue; }
            set { DescriptionValue = value ?? string.Empty; }
        }

        /// <summary>
        /// Gets or sets the action kind performed when the item is confirmed.
        /// </summary>
        public MenuActionKind ActionKind { get; set; }

        /// <summary>
        /// Gets or sets the action target id or scene path.
        /// </summary>
        public string TargetId {
            get { return TargetIdValue; }
            set { TargetIdValue = value ?? string.Empty; }
        }

        /// <summary>
        /// Gets or sets the idle fill color applied to the baked row background.
        /// </summary>
        public byte4 IdleFillColor { get; set; }

        /// <summary>
        /// Gets or sets the idle border color applied to the baked row background.
        /// </summary>
        public byte4 IdleBorderColor { get; set; }

        /// <summary>
        /// Gets or sets the selected fill color applied to the baked row background.
        /// </summary>
        public byte4 SelectedFillColor { get; set; }

        /// <summary>
        /// Gets or sets the selected border color applied to the baked row background.
        /// </summary>
        public byte4 SelectedBorderColor { get; set; }
    }
}
