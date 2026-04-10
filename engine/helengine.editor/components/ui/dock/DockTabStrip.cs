using System;
using System.Collections.Generic;

namespace helengine.editor {
    /// <summary>
    /// Renders a tab strip for a group of docked windows and handles tab selection.
    /// </summary>
    public sealed class DockTabStrip : EditorEntity {
        /// <summary>
        /// Height of the tab strip, 2px shorter than title bar to leave room for separators.
        /// </summary>
        public const int TabHeight = DockableEntity.TitleBarHeight - 2;

        /// <summary>
        /// Horizontal padding inside each tab.
        /// </summary>
        const int TabPaddingX = 12;

        /// <summary>
        /// Minimum width for a tab to keep labels readable.
        /// </summary>
        const int TabMinWidth = 64;

        /// <summary>
        /// Spacing between adjacent tabs.
        /// </summary>
        const int TabSpacing = 4;

        /// <summary>
        /// Left padding before the first tab.
        /// </summary>
        const int TabStripPadding = 6;

        /// <summary>
        /// Render order used by tab backgrounds to ensure they sit above title bars.
        /// </summary>
        readonly byte tabBackgroundOrder;

        /// <summary>
        /// Render order used by tab labels.
        /// </summary>
        readonly byte tabTextOrder;
        /// <summary>
        /// Minimum drag distance before undocking a tab.
        /// </summary>
        const int DragThreshold = DockableEntity.DragUndockThreshold;

        /// <summary>
        /// Font used to render tab labels.
        /// </summary>
        readonly FontAsset font;

        /// <summary>
        /// Collection of tab entries and their visuals.
        /// </summary>
        readonly List<DockTabEntry> tabs;

        /// <summary>
        /// Callback invoked when a tab is selected.
        /// </summary>
        readonly Action<int> onTabSelected;

        /// <summary>
        /// Index of the currently active tab.
        /// </summary>
        int activeIndex;
        /// <summary>
        /// Tracks whether a pointer press is active on a tab.
        /// </summary>
        bool isPointerDown;
        /// <summary>
        /// Tracks whether a tab is currently being dragged to undock.
        /// </summary>
        bool isDragging;
        /// <summary>
        /// Accumulated drag delta since press.
        /// </summary>
        int2 dragDelta;
        /// <summary>
        /// Tab entry currently being pressed or dragged.
        /// </summary>
        DockTabEntry? pressedEntry;

        /// <summary>
        /// Initializes a new tab strip for docked windows.
        /// </summary>
        /// <param name="font">Font used for tab labels.</param>
        /// <param name="onTabSelected">Callback invoked when a tab is selected.</param>
        public DockTabStrip(FontAsset font, Action<int> onTabSelected) {
            this.font = font;
            this.onTabSelected = onTabSelected;
            tabs = new List<DockTabEntry>(4);
            activeIndex = -1;
            isPointerDown = false;
            isDragging = false;
            dragDelta = new int2(0, 0);
            tabBackgroundOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(2);
            tabTextOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(3);
            InternalEntity = true;
            Enabled = false;
        }

        /// <summary>
        /// Updates the tab strip visuals and layout for the provided dockables.
        /// </summary>
        /// <param name="dockables">Dockable windows represented by the strip.</param>
        /// <param name="currentActiveIndex">Index of the active tab.</param>
        /// <param name="position">Top-left position of the strip.</param>
        /// <param name="width">Available width for tabs.</param>
        /// <param name="layerMask">Layer mask to apply to tab visuals.</param>
        public void UpdateTabs(
            IReadOnlyList<DockableEntity> dockables,
            int currentActiveIndex,
            float3 position,
            int width,
            ushort layerMask) {
            if (dockables == null || dockables.Count <= 1) {
                Hide();
                return;
            }

            if (!Enabled) {
                Enabled = true;
            }

            LayerMask = layerMask;
            Position = new float3(position.X, position.Y, position.Z + 0.2f);

            EnsureTabCount(dockables, layerMask);

            activeIndex = Math.Clamp(currentActiveIndex, 0, dockables.Count - 1);

            float x = TabStripPadding;
            for (int i = 0; i < dockables.Count; i++) {
                DockableEntity dockable = dockables[i];
                DockTabEntry entry = tabs[i];
                entry.Dockable = dockable;
                entry.Index = i;
                entry.Root.Enabled = true;
                entry.Root.LayerMask = layerMask;
                entry.LabelHost.LayerMask = layerMask;

                string label = dockable.Title;
                var metrics = font.MeasureTight(label);
                int tabWidth = Math.Max(TabMinWidth, (int)MathF.Ceiling(metrics.Width) + TabPaddingX * 2);

                entry.Width = tabWidth;
                entry.Root.Position = new float3(x, 0, 0.1f);
                entry.Background.Size = new int2(tabWidth, TabHeight);
                entry.Interactable.Size = new int2(tabWidth, TabHeight);
                entry.Label.Text = label;
                entry.Label.Size = new int2((int)MathF.Ceiling(metrics.Width), (int)MathF.Ceiling(metrics.Height));

                float labelX = MathF.Round((tabWidth - metrics.Width) * 0.5f);
                float labelY = GetTextTopOffset(TabHeight);
                entry.LabelHost.Position = new float3(labelX, labelY, 0.2f);

                UpdateTabVisual(entry, i == activeIndex);
                x += tabWidth + TabSpacing;
                if (x > width) {
                    x = width;
                }
            }

            for (int i = dockables.Count; i < tabs.Count; i++) {
                tabs[i].Root.Enabled = false;
            }
        }

        /// <summary>
        /// Hides the tab strip and disables its visuals.
        /// </summary>
        public void Hide() {
            if (Enabled) {
                Enabled = false;
            }
        }

        /// <summary>
        /// Ensures the tab list contains enough entries for the dockables.
        /// </summary>
        /// <param name="count">Required tab count.</param>
        /// <param name="layerMask">Layer mask applied to new tabs.</param>
        void EnsureTabCount(IReadOnlyList<DockableEntity> dockables, ushort layerMask) {
            for (int i = tabs.Count; i < dockables.Count; i++) {
                var entry = new DockTabEntry(dockables[i], font, layerMask, tabBackgroundOrder, tabTextOrder);
                entry.Root.LayerMask = layerMask;
                entry.LabelHost.LayerMask = layerMask;
                entry.Interactable.CursorEvent += (pos, delta, state) => HandleTabCursor(entry, pos, delta, state);
                AddChild(entry.Root);
                entry.Root.Enabled = Enabled;
                tabs.Add(entry);
            }
        }

        /// <summary>
        /// Handles cursor interaction for a tab entry and updates selection.
        /// </summary>
        /// <param name="entry">Tab entry receiving the interaction.</param>
        /// <param name="pos">Pointer position relative to the tab.</param>
        /// <param name="delta">Pointer delta since the last event.</param>
        /// <param name="state">Pointer interaction state.</param>
        void HandleTabCursor(DockTabEntry entry, int2 pos, int2 delta, PointerInteraction state) {
            switch (state) {
                case PointerInteraction.Hover:
                    entry.IsHovering = true;
                    if (isPointerDown && pressedEntry == entry) {
                        if (!isDragging) {
                            dragDelta = new int2(dragDelta.X + delta.X, dragDelta.Y + delta.Y);
                            int dx = dragDelta.X;
                            int dy = dragDelta.Y;
                            int distanceSquared = dx * dx + dy * dy;
                            if (distanceSquared >= DragThreshold * DragThreshold) {
                                isDragging = true;
                                entry.Dockable.BeginExternalDrag();
                            }
                        }

                        if (isDragging) {
                            entry.Dockable.UpdateExternalDrag(delta);
                        }
                    }
                    break;
                case PointerInteraction.Press:
                    isPointerDown = true;
                    isDragging = false;
                    pressedEntry = entry;
                    dragDelta = new int2(0, 0);
                    entry.IsPressed = true;
                    break;
                case PointerInteraction.Release:
                    if (isDragging) {
                        entry.Dockable.EndExternalDrag();
                        isDragging = false;
                        isPointerDown = false;
                        pressedEntry = null;
                        dragDelta = new int2(0, 0);
                        entry.IsPressed = false;
                        entry.IsHovering = false;
                        UpdateAllTabVisuals();
                        return;
                    }

                    bool shouldActivate = entry.IsPressed && entry.IsHovering;
                    entry.IsPressed = false;
                    isPointerDown = false;
                    pressedEntry = null;
                    dragDelta = new int2(0, 0);
                    if (shouldActivate && entry.Index != activeIndex) {
                        activeIndex = entry.Index;
                        onTabSelected(entry.Index);
                        UpdateAllTabVisuals();
                        return;
                    }
                    break;
                case PointerInteraction.Leave:
                    entry.IsHovering = false;
                    entry.IsPressed = false;
                    if (isPointerDown && pressedEntry == entry && isDragging) {
                        entry.Dockable.EndExternalDrag();
                        isDragging = false;
                        isPointerDown = false;
                        pressedEntry = null;
                        dragDelta = new int2(0, 0);
                        UpdateAllTabVisuals();
                        return;
                    }
                    if (pressedEntry == entry && !isDragging) {
                        isPointerDown = false;
                        pressedEntry = null;
                        dragDelta = new int2(0, 0);
                    }
                    break;
                default:
                    break;
            }

            UpdateTabVisual(entry, entry.Index == activeIndex);
        }

        /// <summary>
        /// Updates visuals for all tabs based on the current active index.
        /// </summary>
        void UpdateAllTabVisuals() {
            for (int i = 0; i < tabs.Count; i++) {
                DockTabEntry entry = tabs[i];
                if (!entry.Root.Enabled) {
                    continue;
                }
                UpdateTabVisual(entry, entry.Index == activeIndex);
            }
        }

        /// <summary>
        /// Updates the background and text colors for a tab.
        /// </summary>
        /// <param name="entry">Tab entry to update.</param>
        /// <param name="isActive">True when the tab is active.</param>
        void UpdateTabVisual(DockTabEntry entry, bool isActive) {
            if (entry.IsPressed) {
                entry.Background.Color = ThemeManager.Colors.AccentTertiary;
            } else if (isActive) {
                entry.Background.Color = ThemeManager.Colors.SurfacePrimary;
            } else if (entry.IsHovering) {
                entry.Background.Color = ThemeManager.Colors.AccentSecondary;
            } else {
                entry.Background.Color = ThemeManager.Colors.AccentPrimary;
            }

            entry.Label.Color = isActive ? ThemeManager.Colors.AccentQuaternary : ThemeManager.Colors.TextOnAccent;
        }

        /// <summary>
        /// Computes the vertical offset needed to center tab labels consistently.
        /// </summary>
        /// <param name="containerHeight">Height of the container in pixels.</param>
        /// <returns>Top offset to position the label.</returns>
        float GetTextTopOffset(float containerHeight) {
            float lineHeight = Math.Max(font.LineHeight, 1f);
            return MathF.Round((containerHeight - lineHeight) * 0.5f);
        }
    }
}
