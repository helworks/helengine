using System;
using System.Collections.Generic;

namespace helengine.editor {
    /// <summary>
    /// Lays out dockable editor entities within a host area according to their docking preferences.
    /// </summary>
    public class DockLayoutEngine {
        readonly List<DockableEntity> dockables;
        readonly List<DockableEntity> fillBuffer;
        readonly int padding;
        readonly int gap;

        /// <summary>
        /// Creates a new layout engine with optional padding and gap values.
        /// </summary>
        /// <param name="padding">Space to leave around the host bounds.</param>
        /// <param name="gap">Space inserted between docked panels.</param>
        public DockLayoutEngine(int padding = 0, int gap = 0) {
            this.padding = padding;
            this.gap = gap;
            dockables = new List<DockableEntity>(8);
            fillBuffer = new List<DockableEntity>(2);
        }

        /// <summary>
        /// Gets the dockable entities currently managed by the layout engine.
        /// </summary>
        public IReadOnlyList<DockableEntity> Dockables => dockables;

        /// <summary>
        /// Adds a dockable entity to the layout list if it is not already tracked.
        /// </summary>
        /// <param name="entity">Entity to add.</param>
        public void Add(DockableEntity entity) {
            if (!dockables.Contains(entity)) {
                dockables.Add(entity);
            }
        }

        /// <summary>
        /// Removes a dockable entity from the layout list.
        /// </summary>
        /// <param name="entity">Entity to remove.</param>
        /// <returns>True if the entity was removed; otherwise false.</returns>
        public bool Remove(DockableEntity entity) {
            return dockables.Remove(entity);
        }

        /// <summary>
        /// Performs layout using the host size at the origin.
        /// </summary>
        /// <param name="hostSize">Size of the host area.</param>
        public void Layout(int2 hostSize) {
            Layout(hostSize, float3.Zero);
        }

        /// <summary>
        /// Performs layout using the host size and a specified origin.
        /// </summary>
        /// <param name="hostSize">Size of the host area.</param>
        /// <param name="origin">Origin point of the host area.</param>
        public void Layout(int2 hostSize, float3 origin) {
            float left = origin.X + padding;
            float top = origin.Y + padding;
            float right = origin.X + hostSize.X - padding;
            float bottom = origin.Y + hostSize.Y - padding;

            if (right <= left || bottom <= top) {
                return;
            }

            fillBuffer.Clear();

            for (int i = 0; i < dockables.Count; i++) {
                DockableEntity entity = dockables[i];
                switch (entity.Dock) {
                    case DockRegion.Floating:
                        continue;
                    case DockRegion.Fill:
                        fillBuffer.Add(entity);
                        continue;
                    case DockRegion.Left:
                        layoutLeft(entity, ref left, top, bottom, origin.Z, right);
                        break;
                    case DockRegion.Right:
                        layoutRight(entity, left, top, ref right, bottom, origin.Z);
                        break;
                    case DockRegion.Top:
                        layoutTop(entity, left, ref top, right, ref bottom, origin.Z);
                        break;
                    case DockRegion.Bottom:
                        layoutBottom(entity, left, top, right, ref bottom, origin.Z);
                        break;
                }
            }

            for (int i = 0; i < fillBuffer.Count; i++) {
                DockableEntity entity = fillBuffer[i];
                layoutFill(entity, left, top, right, bottom, origin.Z);
            }
        }

        /// <summary>
        /// Lays out a panel docked to the left and adjusts the remaining available space.
        /// </summary>
        /// <param name="entity">Entity to lay out.</param>
        /// <param name="left">Current left boundary; updated to new boundary.</param>
        /// <param name="top">Top boundary of the host area.</param>
        /// <param name="bottom">Bottom boundary of the host area.</param>
        /// <param name="z">Z coordinate for positioning.</param>
        /// <param name="right">Right boundary of the host area.</param>
        void layoutLeft(DockableEntity entity, ref float left, float top, float bottom, float z, float right) {
            float availableWidth = right - left;
            if (availableWidth <= 0f) {
                return;
            }

            int desiredWidth = Math.Min((int)availableWidth, Math.Max(entity.MinSize.X, entity.Size.X));
            int maxContentHeight = Math.Max(0, (int)(bottom - top - DockableEntity.TitleBarHeight));
            int desiredContentHeight = Math.Max(entity.MinSize.Y, entity.Size.Y);
            int contentHeight = Math.Min(desiredContentHeight, maxContentHeight);

            entity.Position = new float3(left, top, z);
            entity.Size = new int2(desiredWidth, contentHeight);

            left += desiredWidth + gap;
            if (left > right) {
                left = right;
            }
        }

        /// <summary>
        /// Lays out a panel docked to the right and adjusts the remaining available space.
        /// </summary>
        /// <param name="entity">Entity to lay out.</param>
        /// <param name="left">Left boundary of the host area.</param>
        /// <param name="top">Top boundary of the host area.</param>
        /// <param name="right">Current right boundary; updated to new boundary.</param>
        /// <param name="bottom">Bottom boundary of the host area.</param>
        /// <param name="z">Z coordinate for positioning.</param>
        void layoutRight(DockableEntity entity, float left, float top, ref float right, float bottom, float z) {
            float availableWidth = right - left;
            if (availableWidth <= 0f) {
                return;
            }

            int desiredWidth = Math.Min((int)availableWidth, Math.Max(entity.MinSize.X, entity.Size.X));
            int maxContentHeight = Math.Max(0, (int)(bottom - top - DockableEntity.TitleBarHeight));
            int desiredContentHeight = Math.Max(entity.MinSize.Y, entity.Size.Y);
            int contentHeight = Math.Min(desiredContentHeight, maxContentHeight);

            right -= desiredWidth;

            entity.Position = new float3(right, top, z);
            entity.Size = new int2(desiredWidth, contentHeight);

            right -= gap;
            if (right < left) {
                right = left;
            }
        }

        /// <summary>
        /// Lays out a panel docked to the top and adjusts the remaining available space.
        /// </summary>
        /// <param name="entity">Entity to lay out.</param>
        /// <param name="left">Left boundary of the host area.</param>
        /// <param name="top">Current top boundary; updated to new boundary.</param>
        /// <param name="right">Right boundary of the host area.</param>
        /// <param name="bottom">Current bottom boundary; updated to new boundary.</param>
        /// <param name="z">Z coordinate for positioning.</param>
        void layoutTop(DockableEntity entity, float left, ref float top, float right, ref float bottom, float z) {
            float availableHeight = bottom - top;
            if (availableHeight <= 0f) {
                return;
            }

            int fullDesiredHeight = Math.Max(entity.MinSize.Y + DockableEntity.TitleBarHeight, entity.Size.Y + DockableEntity.TitleBarHeight);
            int height = Math.Min((int)availableHeight, fullDesiredHeight);
            int contentHeight = Math.Max(0, height - DockableEntity.TitleBarHeight);
            int width = Math.Max(0, (int)(right - left));

            entity.Position = new float3(left, top, z);
            entity.Size = new int2(width, contentHeight);

            top += height + gap;
            if (top > bottom) {
                top = bottom;
            }
        }

        /// <summary>
        /// Lays out a panel docked to the bottom and adjusts the remaining available space.
        /// </summary>
        /// <param name="entity">Entity to lay out.</param>
        /// <param name="left">Left boundary of the host area.</param>
        /// <param name="top">Top boundary of the host area.</param>
        /// <param name="right">Right boundary of the host area.</param>
        /// <param name="bottom">Current bottom boundary; updated to new boundary.</param>
        /// <param name="z">Z coordinate for positioning.</param>
        void layoutBottom(DockableEntity entity, float left, float top, float right, ref float bottom, float z) {
            float availableHeight = bottom - top;
            if (availableHeight <= 0f) {
                return;
            }

            int fullDesiredHeight = Math.Max(entity.MinSize.Y + DockableEntity.TitleBarHeight, entity.Size.Y + DockableEntity.TitleBarHeight);
            int height = Math.Min((int)availableHeight, fullDesiredHeight);
            int contentHeight = Math.Max(0, height - DockableEntity.TitleBarHeight);
            int width = Math.Max(0, (int)(right - left));

            bottom -= height;

            entity.Position = new float3(left, bottom, z);
            entity.Size = new int2(width, contentHeight);

            bottom -= gap;
            if (bottom < top) {
                bottom = top;
            }
        }

        /// <summary>
        /// Lays out a panel that fills the remaining space.
        /// </summary>
        /// <param name="entity">Entity to lay out.</param>
        /// <param name="left">Left boundary of remaining space.</param>
        /// <param name="top">Top boundary of remaining space.</param>
        /// <param name="right">Right boundary of remaining space.</param>
        /// <param name="bottom">Bottom boundary of remaining space.</param>
        /// <param name="z">Z coordinate for positioning.</param>
        void layoutFill(DockableEntity entity, float left, float top, float right, float bottom, float z) {
            int width = Math.Max(0, (int)(right - left));
            int height = Math.Max(0, (int)(bottom - top - DockableEntity.TitleBarHeight));

            entity.Position = new float3(left, top, z);
            entity.Size = new int2(width, height);
        }

        /// <summary>
        /// Calculates a dock hint based on a pointer position within the host area.
        /// </summary>
        /// <param name="pointer">Pointer position in screen or host coordinates.</param>
        /// <param name="hostSize">Size of the host area.</param>
        /// <param name="origin">Origin of the host area.</param>
        /// <param name="fillOnly">True to only allow fill hints.</param>
        /// <param name="region">Resulting suggested docking region.</param>
        /// <param name="position">Resulting top-left position for the hint region.</param>
        /// <param name="size">Resulting size for the hint region.</param>
        /// <returns>True if a dock hint could be determined; otherwise false.</returns>
        public bool TryGetDockHint(int2 pointer, int2 hostSize, float3 origin, bool fillOnly, out DockRegion region, out float3 position, out int2 size) {
            region = DockRegion.Floating;
            position = origin;
            size = hostSize;

            if (hostSize.X <= 0 || hostSize.Y <= 0) {
                return false;
            }

            float localX = pointer.X - origin.X;
            float localY = pointer.Y - origin.Y;

            if (localX < 0 || localY < 0 || localX > hostSize.X || localY > hostSize.Y) {
                return false;
            }

            if (fillOnly) {
                int centerWidth = Math.Max(32, hostSize.X / 3);
                int centerHeight = Math.Max(32, hostSize.Y / 3);

                float centerStartX = origin.X + (hostSize.X - centerWidth) * 0.5f;
                float centerStartY = origin.Y + (hostSize.Y - centerHeight) * 0.5f;
                float centerEndX = centerStartX + centerWidth;
                float centerEndY = centerStartY + centerHeight;

                if (pointer.X >= centerStartX && pointer.X <= centerEndX &&
                    pointer.Y >= centerStartY && pointer.Y <= centerEndY) {
                    region = DockRegion.Fill;
                    position = origin;
                    size = hostSize;
                    return true;
                }

                return false;
            }

            int leftThreshold = Math.Max(64, hostSize.X / 5);
            int rightThreshold = hostSize.X - leftThreshold;
            int topThreshold = Math.Max(64, hostSize.Y / 5);
            int bottomThreshold = hostSize.Y - topThreshold;

            if (localX <= leftThreshold) {
                region = DockRegion.Left;
                int width = Math.Max(1, hostSize.X / 3);
                position = origin;
                size = new int2(width, hostSize.Y);
                return true;
            }

            if (localX >= rightThreshold) {
                region = DockRegion.Right;
                int width = Math.Max(1, hostSize.X / 3);
                position = new float3(origin.X + hostSize.X - width, origin.Y, origin.Z);
                size = new int2(width, hostSize.Y);
                return true;
            }

            if (localY <= topThreshold) {
                region = DockRegion.Top;
                int height = Math.Max(1, hostSize.Y / 3);
                position = origin;
                size = new int2(hostSize.X, height);
                return true;
            }

            if (localY >= bottomThreshold) {
                region = DockRegion.Bottom;
                int height = Math.Max(1, hostSize.Y / 3);
                position = new float3(origin.X, origin.Y + hostSize.Y - height, origin.Z);
                size = new int2(hostSize.X, height);
                return true;
            }

            region = DockRegion.Fill;
            position = origin;
            size = hostSize;
            return true;
        }
    }
}
