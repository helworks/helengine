using System;
using System.Collections.Generic;

namespace helengine.editor {
    public class DockLayoutEngine {
        readonly List<DockableEntity> dockables;
        readonly List<DockableEntity> fillBuffer;
        readonly int padding;
        readonly int gap;

        public DockLayoutEngine(int padding = 8, int gap = 6) {
            this.padding = padding;
            this.gap = gap;
            dockables = new List<DockableEntity>(8);
            fillBuffer = new List<DockableEntity>(2);
        }

        public void Add(DockableEntity entity) {
            if (!dockables.Contains(entity)) {
                dockables.Add(entity);
            }
        }

        public bool Remove(DockableEntity entity) {
            return dockables.Remove(entity);
        }

        public void Layout(int2 hostSize) {
            Layout(hostSize, float3.Zero);
        }

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

        void layoutFill(DockableEntity entity, float left, float top, float right, float bottom, float z) {
            int width = Math.Max(0, (int)(right - left));
            int height = Math.Max(0, (int)(bottom - top - DockableEntity.TitleBarHeight));

            entity.Position = new float3(left, top, z);
            entity.Size = new int2(width, height);
        }
    }
}
