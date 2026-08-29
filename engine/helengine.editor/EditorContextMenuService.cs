namespace helengine.editor {
    /// <summary>
    /// Owns context-menu visibility and presentation state for one editor session.
    /// </summary>
    public sealed class EditorContextMenuService : IDisposable {
        readonly List<ContextMenu> visibleMenus = new List<ContextMenu>();
        string submenuIndicator = "v";

        internal string SubmenuIndicator {
            get { return submenuIndicator; }
            set { submenuIndicator = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        internal bool Contains(ContextMenu menu) {
            return visibleMenus.Contains(menu);
        }

        internal void Register(ContextMenu menu) {
            if (menu == null) {
                throw new ArgumentNullException(nameof(menu));
            }
            if (!visibleMenus.Contains(menu)) {
                visibleMenus.Add(menu);
            }
        }

        internal void Unregister(ContextMenu menu) {
            visibleMenus.Remove(menu);
        }

        internal bool ContainsOtherMenuAt(ContextMenu requestingMenu, int2 pointer) {
            for (int index = 0; index < visibleMenus.Count; index++) {
                ContextMenu menu = visibleMenus[index];
                if (menu == null || ReferenceEquals(menu, requestingMenu) || !menu.IsVisible) {
                    continue;
                }
                if (menu.ContainsPointer(pointer)) {
                    return true;
                }
            }
            return false;
        }

        public void Dispose() {
            visibleMenus.Clear();
        }
    }
}
